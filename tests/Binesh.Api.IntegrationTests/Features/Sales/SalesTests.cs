using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.Sales.ListSales;
using Binesh.Application.Features.Sales.Shared;
using Binesh.Domain.Customers;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Sales;

/// <summary>
/// Integration tests for the Sales CRUD slices added in Round 9.
/// Panel summary endpoints (Categorized/Regional/TopSelling) get their own
/// test file in Round 9b.
/// </summary>
public sealed class SalesTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989122222222";
    private readonly HttpClient _client = factory.CreateClient();

    private Guid _productId;
    private Guid _customerId;
    private Guid _otherProductId;
    private Guid _otherCustomerId;

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
        await SeedFixtureAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Create_HappyPath_Returns201WithFullSale()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 5000L,
            quantity = 2f,
            deliveredQuantity = 2f,
            docNumber = 555,
            productId = _productId,
            counterpartyId = _customerId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SaleDto>();
        Assert.NotNull(dto);
        Assert.Equal(5000L, dto!.Price);
        Assert.Equal(2f, dto.Quantity);
        Assert.Equal(2f, dto.DeliveredQuantity);
        Assert.Equal(555, dto.DocNumber);
        Assert.Equal(_productId, dto.Product.Id);
        Assert.Equal(_customerId, dto.Counterparty.Id);
        Assert.Equal("PROD-1", dto.Product.ProductCode);
    }

    [Fact]
    public async Task Create_UnknownProduct_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 100L,
            quantity = 1f,
            deliveredQuantity = 1f,
            docNumber = 1,
            productId = Guid.NewGuid(),
            counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_UnknownCustomer_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 100L,
            quantity = 1f,
            deliveredQuantity = 1f,
            docNumber = 1,
            productId = _productId,
            counterpartyId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_NegativePrice_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            date = "2026-03-15T10:00:00Z",
            price = -1L,
            quantity = 1f,
            deliveredQuantity = 1f,
            docNumber = 1,
            productId = _productId,
            counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Create_ZeroQuantity_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/sales", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 100L,
            quantity = 0f,
            deliveredQuantity = 0f,
            docNumber = 1,
            productId = _productId,
            counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var created = await CreateSaleAsync(price: 333);
        var response = await _client.GetAsync($"/api/sales/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SaleDto>();
        Assert.Equal(333L, dto!.Price);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/sales/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_NoQueryParams_DefaultsToPage1Size20()
    {
        var response = await _client.GetAsync("/api/sales");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ListSalesResponse>();
        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task List_FilterByCustomer_OnlyMatchingReturned()
    {
        await CreateSaleAsync(customerId: _customerId, price: 100);
        await CreateSaleAsync(customerId: _otherCustomerId, price: 200);
        await CreateSaleAsync(customerId: _customerId, price: 300);

        var response = await _client.GetAsync($"/api/sales?customerId={_customerId}");
        var page = await response.Content.ReadFromJsonAsync<ListSalesResponse>();
        Assert.Equal(2, page!.Items.Count);
        Assert.All(page.Items, s => Assert.Equal(_customerId, s.Counterparty.Id));
    }

    [Fact]
    public async Task List_FilterByProduct_OnlyMatchingReturned()
    {
        await CreateSaleAsync(productId: _productId);
        await CreateSaleAsync(productId: _otherProductId);

        var response = await _client.GetAsync($"/api/sales?productId={_otherProductId}");
        var page = await response.Content.ReadFromJsonAsync<ListSalesResponse>();
        Assert.Single(page!.Items);
        Assert.Equal(_otherProductId, page.Items[0].Product.Id);
    }

    [Fact]
    public async Task List_FilterByDateRange_OnlyInRangeReturned()
    {
        await CreateSaleAsync(date: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateSaleAsync(date: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        await CreateSaleAsync(date: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        var response = await _client.GetAsync("/api/sales?from=2026-02-01&to=2026-05-01");
        var page = await response.Content.ReadFromJsonAsync<ListSalesResponse>();
        Assert.Single(page!.Items);
        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), page.Items[0].Date);
    }

    [Fact]
    public async Task List_SearchByProductCode_MatchesPartial()
    {
        await CreateSaleAsync(productId: _productId);       // PROD-1
        await CreateSaleAsync(productId: _otherProductId);  // PROD-2

        var response = await _client.GetAsync("/api/sales?search=PROD-2");
        var page = await response.Content.ReadFromJsonAsync<ListSalesResponse>();
        Assert.Single(page!.Items);
        Assert.Equal("PROD-2", page.Items[0].Product.ProductCode);
    }

    [Fact]
    public async Task Update_PartialPatch_UpdatesOnlySetFields()
    {
        var created = await CreateSaleAsync(price: 100, quantity: 1, docNumber: 7);

        var response = await _client.PutAsJsonAsync($"/api/sales/{created.Id}", new
        {
            price = 999L,
            // docNumber, quantity, dates etc. unset → unchanged
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SaleDto>();
        Assert.Equal(999L, updated!.Price);
        Assert.Equal(1f, updated.Quantity);   // unchanged
        Assert.Equal(7, updated.DocNumber);   // unchanged
    }

    [Fact]
    public async Task Update_ChangeFkToUnknownProduct_Returns404()
    {
        var created = await CreateSaleAsync();
        var response = await _client.PutAsJsonAsync($"/api/sales/{created.Id}", new
        {
            productId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/sales/{Guid.NewGuid()}", new { price = 1L });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_Returns204()
    {
        var created = await CreateSaleAsync();

        var response = await _client.DeleteAsync($"/api/sales/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.Sales.SingleOrDefaultAsync(s => s.Id == created.Id));
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/sales/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/sales");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<SaleDto> CreateSaleAsync(
        Guid? productId = null,
        Guid? customerId = null,
        long price = 1000,
        float quantity = 1f,
        int docNumber = 1,
        DateTime? date = null)
    {
        var body = new
        {
            date = date ?? new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc),
            price,
            quantity,
            deliveredQuantity = quantity,
            docNumber,
            productId = productId ?? _productId,
            counterpartyId = customerId ?? _customerId,
        };
        var response = await _client.PostAsJsonAsync("/api/sales", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaleDto>())!;
    }

    private async Task SeedFixtureAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var product1 = Product.Create(ProductType.Carpet, "PROD-1", "Carpet line 1", "600 reed");
        var product2 = Product.Create(ProductType.Rug, "PROD-2", "Rug line 2", "small");

        var person1 = Person.Create("Ali", "Ahmadi", null, null, "09121111111", null, null, null, null, null);
        var person2 = Person.Create("Sara", "Karimi", null, null, "09122222222", null, null, null, null, null);
        var c1 = Customer.Create(CustomerType.MoshtarianKhanegi, true, 0.8f, person1);
        var c2 = Customer.Create(CustomerType.Bedehkaran, true, 0.5f, person2);

        db.Products.AddRange(product1, product2);
        db.Customers.AddRange(c1, c2);
        await db.SaveChangesAsync();

        _productId = product1.Id;
        _otherProductId = product2.Id;
        _customerId = c1.Id;
        _otherCustomerId = c2.Id;
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
        await db.Sales.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Regions.ExecuteDeleteAsync();
        await db.InventoryEvents.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        foreach (var u in await userManager.Users
                     .Where(u => u.PhoneNumber != BineshApiFactory.SuperAdminPhone)
                     .ToListAsync())
        {
            await userManager.DeleteAsync(u);
        }
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        var sa = await userManager.Users.SingleAsync(u => u.PhoneNumber == BineshApiFactory.SuperAdminPhone);
        sa.LastOtpRequestedAt = null;
        await userManager.UpdateAsync(sa);
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
