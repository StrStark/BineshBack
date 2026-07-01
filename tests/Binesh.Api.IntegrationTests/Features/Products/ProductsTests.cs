using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.Products.GetStagnationReport;
using Binesh.Application.Features.Products.ListInventoryEvents;
using Binesh.Application.Features.Products.ListProducts;
using Binesh.Application.Features.Products.Shared;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Products;

public sealed class ProductsTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989121111111";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── CRUD ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_HappyPath_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            type = ProductType.Carpet,
            productCode = "CARPET-001",
            productDescription = "12-meter handwoven carpet",
            detailedType = "Tabriz 70-raj",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(ProductType.Carpet, dto!.Type);
        Assert.Equal("CARPET-001", dto.ProductCode);
    }

    [Fact]
    public async Task Create_DuplicateCode_Returns409()
    {
        await CreateProductAsync("DUP-001");
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            type = ProductType.Carpet,
            productCode = "DUP-001",
            productDescription = "Another with same code",
            detailedType = "X",
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetByCode_Existing_ReturnsProduct()
    {
        var created = await CreateProductAsync("LOOKUP-CODE");
        var response = await _client.GetAsync($"/api/products/by-code/LOOKUP-CODE");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal(created.Id, dto!.Id);
    }

    [Fact]
    public async Task List_FilterByType_OnlyMatching()
    {
        await CreateProductAsync("A1", type: ProductType.Carpet);
        await CreateProductAsync("B1", type: ProductType.RawMaterials);
        await CreateProductAsync("C1", type: ProductType.Carpet);

        var response = await _client.GetAsync($"/api/products?type={ProductType.Carpet}");
        var page = await response.Content.ReadFromJsonAsync<ListProductsResponse>();
        Assert.Equal(2, page!.Items.Count);
        Assert.All(page.Items, p => Assert.Equal(ProductType.Carpet, p.Type));
    }

    [Fact]
    public async Task List_WithStats_AggregatesFromEvents()
    {
        var product = await CreateProductAsync("STATS-1");
        await AddEventAsync(product.Id, InventoryEventType.Receipt, qty: 10, unitPrice: 100, totalPrice: 1000);
        await AddEventAsync(product.Id, InventoryEventType.SalesInvoice, qty: 3, unitPrice: 150, totalPrice: 450);

        var response = await _client.GetAsync($"/api/products?includeStats=true");
        var page = await response.Content.ReadFromJsonAsync<ListProductsResponse>();
        var item = page!.ItemsWithStats!.Single(p => p.ProductCode == "STATS-1");
        Assert.Equal(250L, item.TotalUnitPriceSum);     // 100 + 150
        Assert.Equal(1450L, item.TotalRevenueSum);      // 1000 + 450
        Assert.Equal(2, item.EventCount);
    }

    [Fact]
    public async Task Update_PartialPatch_Works()
    {
        var product = await CreateProductAsync("PATCH-1", description: "Original");

        var response = await _client.PutAsJsonAsync($"/api/products/{product.Id}", new
        {
            productDescription = "Updated",
            // type / code / detailedType all unset → unchanged
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProductDto>();
        Assert.Equal("Updated", dto!.ProductDescription);
        Assert.Equal("PATCH-1", dto.ProductCode);
        Assert.Equal(product.Type, dto.Type);
    }

    [Fact]
    public async Task Update_ChangeCodeToExistingOne_Returns409()
    {
        await CreateProductAsync("EXISTING");
        var second = await CreateProductAsync("SECOND");

        var response = await _client.PutAsJsonAsync($"/api/products/{second.Id}", new
        {
            productCode = "EXISTING",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Cascade_RemovesEvents()
    {
        var product = await CreateProductAsync("DEL-1");
        await AddEventAsync(product.Id, InventoryEventType.Receipt, qty: 5);

        var del = await _client.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
        Assert.False(await db.InventoryEvents.AnyAsync(e => e.ProductId == product.Id));
    }

    // ── Inventory events ────────────────────────────────────────────────────

    [Fact]
    public async Task AddEvent_HappyPath_Returns201()
    {
        var product = await CreateProductAsync("EV-1");
        var ev = await AddEventAsync(product.Id, InventoryEventType.Receipt, qty: 10, unitPrice: 50, totalPrice: 500);
        Assert.Equal(InventoryEventType.Receipt, ev.Type);
        Assert.Equal(product.Id, ev.ProductId);
    }

    [Fact]
    public async Task AddEvent_UnknownProduct_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"/api/products/{Guid.NewGuid()}/events", new
        {
            type = InventoryEventType.Receipt,
            date = DateTime.UtcNow,
            quantity = 1f,
            unitPrice = 10L,
            totalPrice = 10L,
            factorNumber = 1,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListEvents_PaginatedDescendingByDate()
    {
        var product = await CreateProductAsync("EV-LIST");
        await AddEventAsync(product.Id, InventoryEventType.Receipt, date: D(2026, 1, 1));
        await AddEventAsync(product.Id, InventoryEventType.Issue, date: D(2026, 3, 1));
        await AddEventAsync(product.Id, InventoryEventType.Receipt, date: D(2026, 2, 1));

        var response = await _client.GetAsync($"/api/products/{product.Id}/events");
        var page = await response.Content.ReadFromJsonAsync<ListInventoryEventsResponse>();
        Assert.Equal(3, page!.TotalCount);
        Assert.Equal(D(2026, 3, 1), page.Items[0].Date);
        Assert.Equal(D(2026, 2, 1), page.Items[1].Date);
        Assert.Equal(D(2026, 1, 1), page.Items[2].Date);
    }

    [Fact]
    public async Task ClearEvents_DeletesAllForProduct_KeepsProduct()
    {
        var product = await CreateProductAsync("CLR-1");
        await AddEventAsync(product.Id, InventoryEventType.Receipt);
        await AddEventAsync(product.Id, InventoryEventType.Issue);

        var del = await _client.DeleteAsync($"/api/products/{product.Id}/events");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.NotNull(await db.Products.SingleOrDefaultAsync(p => p.Id == product.Id));
        Assert.False(await db.InventoryEvents.AnyAsync(e => e.ProductId == product.Id));
    }

    // ── Stagnation FIFO ─────────────────────────────────────────────────────

    [Fact]
    public async Task Stagnation_FifoConsumesOldestFirst_ReportsRemaining()
    {
        // 100-day-old receipt of 10 units, then 50-day-old receipt of 5 units,
        // then a sale of 8 units → 7 units remain: 2 from the old batch + 5 from the new.
        var product = await CreateProductAsync("STAG-1");
        await AddEventAsync(product.Id, InventoryEventType.Receipt,
            date: DateTime.UtcNow.AddDays(-100), qty: 10, unitPrice: 100);
        await AddEventAsync(product.Id, InventoryEventType.Receipt,
            date: DateTime.UtcNow.AddDays(-50), qty: 5, unitPrice: 200);
        await AddEventAsync(product.Id, InventoryEventType.SalesInvoice,
            date: DateTime.UtcNow.AddDays(-10), qty: 8, unitPrice: 250);

        var response = await _client.GetAsync("/api/products/stagnation");
        var report = await response.Content.ReadFromJsonAsync<StagnationReportResponse>();

        var point = report!.Points.Single(p => p.ProductCode == "STAG-1");
        Assert.Equal(7f, point.CurrentStock);                    // 10 + 5 - 8 = 7
        Assert.Equal(200L, point.LatestUnitPrice);               // last Receipt
        Assert.Equal(1400L, point.TotalStagnationValue);         // 7 * 200
        Assert.InRange(point.WeightedAverageAgeDays, 60d, 80d);  // weighted: (2*100 + 5*50)/7 ≈ 64
    }

    [Fact]
    public async Task Stagnation_FullyConsumedProduct_NotInReport()
    {
        var product = await CreateProductAsync("STAG-EMPTY");
        await AddEventAsync(product.Id, InventoryEventType.Receipt,
            date: DateTime.UtcNow.AddDays(-30), qty: 5);
        await AddEventAsync(product.Id, InventoryEventType.SalesInvoice,
            date: DateTime.UtcNow.AddDays(-1), qty: 5);

        var response = await _client.GetAsync("/api/products/stagnation");
        var report = await response.Content.ReadFromJsonAsync<StagnationReportResponse>();

        Assert.DoesNotContain(report!.Points, p => p.ProductCode == "STAG-EMPTY");
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static DateTime D(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private async Task<ProductDto> CreateProductAsync(
        string code,
        ProductType type = ProductType.Carpet,
        string description = "Test product")
    {
        var response = await _client.PostAsJsonAsync("/api/products", new
        {
            type,
            productCode = code,
            productDescription = description,
            detailedType = "test",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ProductDto>())!;
    }

    private async Task<InventoryEventDto> AddEventAsync(
        Guid productId,
        InventoryEventType type,
        DateTime? date = null,
        float qty = 1f,
        long unitPrice = 10L,
        long totalPrice = 10L,
        int factorNumber = 1)
    {
        var response = await _client.PostAsJsonAsync($"/api/products/{productId}/events", new
        {
            type,
            date = date ?? DateTime.UtcNow,
            quantity = qty,
            unitPrice,
            totalPrice,
            factorNumber,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InventoryEventDto>())!;
    }

    private async Task SignInAsync(string phone)
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = phone });
        var otp = factory.Sms.GetLastOtp(phone)!;
        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = phone,
            otp,
            deviceInfo = "xunit",
        });
        verify.EnsureSuccessStatusCode();
        var tokens = (await verify.Content.ReadFromJsonAsync<VerifyOtpResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.InventoryEvents.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastOtpRequestedAt, (DateTimeOffset?)null));
    }

    private async Task EnsureUserAsync(string phone, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.SingleOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user is null)
        {
            user = new User { UserName = phone, PhoneNumber = phone, PhoneNumberConfirmed = true };
            await userManager.CreateAsync(user);
        }
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
