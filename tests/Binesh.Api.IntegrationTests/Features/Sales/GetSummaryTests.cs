using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.Sales.GetSummary;
using Binesh.Domain.Customers;
using Binesh.Domain.Identity;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Sales;

/// <summary>
/// Reference integration test — every other slice gets a sibling file like this.
///
/// Pattern:
///   1. Use BineshApiFactory (real host + Testcontainers Postgres).
///   2. ResetAsync between tests to keep them independent.
///   3. Seed via DbContext, call HTTP, assert response shape + values.
///
/// Round 9 extended Sale with required Product + Counterparty FKs, so this
/// fixture now seeds one of each in InitializeAsync and reuses their ids in
/// the per-test seed calls.
/// </summary>
public sealed class GetSummaryTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989123333333";
    private readonly HttpClient _client = factory.CreateClient();
    private Guid _productId;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetDatabaseAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
        (_productId, _customerId) = await SeedProductAndCustomerAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task EmptyDatabase_ReturnsZeros()
    {
        var response = await _client.GetAsync(
            "/api/sales/summary?from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<GetSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(0, summary!.OrderCount);
        Assert.Equal(0L, summary.TotalRevenue);
        Assert.Equal(0m, summary.AverageOrderValue);
        Assert.Empty(summary.ByDay);
    }

    [Fact]
    public async Task ThreeSalesAcrossTwoDays_AggregatesPerDay()
    {
        await SeedAsync(
            NewSale(D(2026, 1, 5, 12), 1000, 1, 100),
            NewSale(D(2026, 1, 5, 14), 2000, 1, 101),
            NewSale(D(2026, 1, 10, 9), 500, 1, 102));

        var response = await _client.GetAsync(
            "/api/sales/summary?from=2026-01-01&to=2026-01-31");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var summary = await response.Content.ReadFromJsonAsync<GetSummaryResponse>();
        Assert.NotNull(summary);
        Assert.Equal(3, summary!.OrderCount);
        Assert.Equal(3500L, summary.TotalRevenue);
        Assert.Equal(2, summary.ByDay.Count);

        var jan5 = summary.ByDay.Single(d => d.Date == new DateOnly(2026, 1, 5));
        Assert.Equal(3000L, jan5.Revenue);
        Assert.Equal(2, jan5.OrderCount);

        var jan10 = summary.ByDay.Single(d => d.Date == new DateOnly(2026, 1, 10));
        Assert.Equal(500L, jan10.Revenue);
        Assert.Equal(1, jan10.OrderCount);
    }

    [Fact]
    public async Task SalesOutsideRange_AreExcluded()
    {
        await SeedAsync(
            NewSale(D(2025, 12, 31, 23), 9999, 1, 200),    // before
            NewSale(D(2026, 2, 1, 0), 9999, 1, 201));      // after

        var response = await _client.GetAsync(
            "/api/sales/summary?from=2026-01-01&to=2026-01-31");

        var summary = await response.Content.ReadFromJsonAsync<GetSummaryResponse>();
        Assert.Equal(0, summary!.OrderCount);
    }

    [Fact]
    public async Task FromAfterTo_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync(
            "/api/sales/summary?from=2026-02-01&to=2026-01-01");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadAsStringAsync();
        Assert.True(
            problem.Contains("From must be on or before To"),
            $"Expected validation message in body. Got:\n{problem}");
    }

    [Fact]
    public async Task RangeOver366Days_ReturnsValidationProblem()
    {
        var response = await _client.GetAsync(
            "/api/sales/summary?from=2024-01-01&to=2026-01-01");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private Sale NewSale(DateTime date, long price, float qty, int docNumber) =>
        Sale.Create(date, price, qty, qty, docNumber, _productId, _customerId);

    private async Task ResetDatabaseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.Sales.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    private async Task<(Guid productId, Guid customerId)> SeedProductAndCustomerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var product = Product.Create(ProductType.Carpet, "SUMMARY-FIX-1", "Summary fixture product", "x");
        var person = Person.Create("Fixture", "Counterparty", null, null, null, null, null, null, null, null);
        var customer = Customer.Create(CustomerType.MoshtarianKhanegi, true, 0.5f, person);

        db.Products.Add(product);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        return (product.Id, customer.Id);
    }

    private async Task SeedAsync(params Sale[] sales)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        db.Sales.AddRange(sales);
        await db.SaveChangesAsync();
    }

    private static DateTime D(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

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
