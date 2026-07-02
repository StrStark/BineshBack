using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.SalesReturns.GetReturnsSummary;
using Binesh.Application.Features.SalesReturns.ListSalesReturns;
using Binesh.Application.Features.SalesReturns.Shared;
using Binesh.Domain.Customers;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.SalesReturns;

/// <summary>
/// Round 10 — integration tests for SalesReturns CRUD + summary.
/// </summary>
public sealed class SalesReturnsTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989125555555";
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
    public async Task Create_HappyPath_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/sales-returns", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 2500L,
            quantity = 1f,
            deliveredQuantity = 1f,
            docNumber = 99,
            productId = _productId,
            counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SalesReturnDto>();
        Assert.Equal(2500L, dto!.Price);
        Assert.Equal("RP-1", dto.Product.ProductCode);
    }

    [Fact]
    public async Task Create_UnknownProduct_Returns404()
    {
        var response = await _client.PostAsJsonAsync("/api/sales-returns", new
        {
            date = "2026-03-15T10:00:00Z",
            price = 100L, quantity = 1f, deliveredQuantity = 1f, docNumber = 1,
            productId = Guid.NewGuid(), counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_NegativePrice_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/sales-returns", new
        {
            date = "2026-03-15T10:00:00Z",
            price = -1L, quantity = 1f, deliveredQuantity = 1f, docNumber = 1,
            productId = _productId, counterpartyId = _customerId,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Existing_Returns200()
    {
        var created = await CreateReturnAsync(price: 444);
        var response = await _client.GetAsync($"/api/sales-returns/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SalesReturnDto>();
        Assert.Equal(444L, dto!.Price);
    }

    [Fact]
    public async Task GetById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/sales-returns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_NoParams_DefaultsAndReturnsAll()
    {
        await CreateReturnAsync(price: 100);
        await CreateReturnAsync(price: 200);

        var response = await _client.GetAsync("/api/sales-returns");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ListSalesReturnsResponse>();
        Assert.Equal(2, page!.Items.Count);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task List_FilterByCustomer_OnlyMatching()
    {
        await CreateReturnAsync(customerId: _customerId);
        await CreateReturnAsync(customerId: _otherCustomerId);

        var response = await _client.GetAsync($"/api/sales-returns?customerId={_customerId}");
        var page = await response.Content.ReadFromJsonAsync<ListSalesReturnsResponse>();
        Assert.Single(page!.Items);
        Assert.Equal(_customerId, page.Items[0].Counterparty.Id);
    }

    [Fact]
    public async Task List_FilterByDateRange_OnlyInRange()
    {
        await CreateReturnAsync(date: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateReturnAsync(date: new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc));
        await CreateReturnAsync(date: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        var response = await _client.GetAsync("/api/sales-returns?from=2026-02-01&to=2026-05-01");
        var page = await response.Content.ReadFromJsonAsync<ListSalesReturnsResponse>();
        Assert.Single(page!.Items);
    }

    [Fact]
    public async Task Update_PartialPatch_OnlyChangesSetFields()
    {
        var created = await CreateReturnAsync(price: 100, docNumber: 5);
        var response = await _client.PutAsJsonAsync($"/api/sales-returns/{created.Id}", new
        {
            price = 7777L,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<SalesReturnDto>();
        Assert.Equal(7777L, dto!.Price);
        Assert.Equal(5, dto.DocNumber);
    }

    [Fact]
    public async Task Update_NonExistent_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/api/sales-returns/{Guid.NewGuid()}", new { price = 1L });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Existing_Returns204()
    {
        var created = await CreateReturnAsync();
        var response = await _client.DeleteAsync($"/api/sales-returns/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.SalesReturns.SingleOrDefaultAsync(s => s.Id == created.Id));
    }

    [Fact]
    public async Task Delete_NonExistent_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/sales-returns/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_EmptyDatabase_ReturnsZeros()
    {
        var response = await _client.GetAsync("/api/sales-returns/summary?from=2026-01-01&to=2026-01-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetReturnsSummaryResponse>();
        Assert.Equal(0, body!.ReturnCount);
        Assert.Equal(0L, body.TotalReturned);
        Assert.Empty(body.ByDay);
    }

    [Fact]
    public async Task Summary_AggregatesPerDay()
    {
        await CreateReturnAsync(date: D(2026, 3, 5), price: 1000);
        await CreateReturnAsync(date: D(2026, 3, 5), price: 2000);
        await CreateReturnAsync(date: D(2026, 3, 10), price: 500);

        var response = await _client.GetAsync("/api/sales-returns/summary?from=2026-03-01&to=2026-03-31");
        var body = await response.Content.ReadFromJsonAsync<GetReturnsSummaryResponse>();
        Assert.Equal(3, body!.ReturnCount);
        Assert.Equal(3500L, body.TotalReturned);
        Assert.Equal(2, body.ByDay.Count);

        var d5 = body.ByDay.Single(d => d.Date == new DateOnly(2026, 3, 5));
        Assert.Equal(3000L, d5.Returned);
        Assert.Equal(2, d5.ReturnCount);
    }

    [Fact]
    public async Task Summary_InvalidRange_Returns422()
    {
        var response = await _client.GetAsync("/api/sales-returns/summary?from=2026-03-31&to=2026-03-01");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/sales-returns");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<SalesReturnDto> CreateReturnAsync(
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
        var response = await _client.PostAsJsonAsync("/api/sales-returns", body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SalesReturnDto>())!;
    }

    private async Task SeedFixtureAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        var companyId = await db.Companies.Select(c => c.Id).FirstAsync();

        var p1 = Product.Create(companyId, ProductType.Carpet, "RP-1", "Returnable carpet", "600 reed");
        var p2 = Product.Create(companyId, ProductType.Rug, "RP-2", "Returnable rug", "small");

        var per1 = Person.Create("Buyer", "One", null, null, "0911", null, null, null, null, null);
        var per2 = Person.Create("Buyer", "Two", null, null, "0922", null, null, null, null, null);
        var c1 = Customer.Create(companyId, CustomerType.MoshtarianKhanegi, true, 0.8f, per1);
        var c2 = Customer.Create(companyId, CustomerType.Bedehkaran, true, 0.5f, per2);

        db.Products.AddRange(p1, p2);
        db.Customers.AddRange(c1, c2);
        await db.SaveChangesAsync();

        _productId = p1.Id;
        _otherProductId = p2.Id;
        _customerId = c1.Id;
        _otherCustomerId = c2.Id;
    }

    private static DateTime D(int year, int month, int day) =>
        new(year, month, day, 12, 0, 0, DateTimeKind.Utc);

    private async Task SignInAsync(string phone)
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = phone });
        var otp = factory.Sms.GetLastOtp(phone)!;
        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = phone, otp, deviceInfo = "xunit",
        });
        verify.EnsureSuccessStatusCode();
        var tokens = (await verify.Content.ReadFromJsonAsync<VerifyOtpResponse>())!;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.SalesReturns.ExecuteDeleteAsync();
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
