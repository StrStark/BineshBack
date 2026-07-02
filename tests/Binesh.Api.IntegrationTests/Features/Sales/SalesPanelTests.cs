using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
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
/// Parity tests for the legacy <c>SalesApiController</c> panel endpoints, now
/// exposed as POST /api/sales/panel/* with the <see cref="ApiResponse{T}"/>
/// envelope. Each asserts the exact numbers the old business logic produced.
/// </summary>
public sealed class SalesPanelTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989122223333";
    private readonly HttpClient _client = factory.CreateClient();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private Guid _prodA, _prodB;      // DetailedType "A" / "B"
    private Guid _custKhanegi, _custBedehkar;
    private Guid _companyId;

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
        await SeedFixtureAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Summary ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Summary_ComputesSoldItemsReturnPercentAndCards()
    {
        // Window: Mar 2026 (30 days). Previous window auto = Jan 30 .. Mar 1.
        await AddSaleAsync(_prodA, _custKhanegi, 1000, day: "2026-03-10");
        await AddSaleAsync(_prodA, _custKhanegi, 500, day: "2026-03-12");
        await AddSaleAsync(_prodB, _custBedehkar, 2000, day: "2026-03-15");
        await AddSaleAsync(_prodA, _custKhanegi, 300, day: "2026-02-15"); // previous window
        await AddReturnAsync(_prodA, _custKhanegi, 150, day: "2026-03-20");
        await AddReturnAsync(_prodA, _custKhanegi, 50, day: "2026-02-16");  // previous window

        var body = Req("2026-03-01", "2026-03-31");
        var env = await PostAsync<SalesSummaryDto>("/api/sales/panel/summary", body);

        Assert.Equal("success", env.Status);
        Assert.Equal("Sales Fetched successfully", env.Message);
        var dto = env.Body!;

        Assert.Equal(3, dto.Count);         // 3 current sales
        Assert.Equal(3500, dto.Sum);        // 1500 (A) + 2000 (B)

        var a = dto.SoldItems.Single(s => s.Type == "A");
        var b = dto.SoldItems.Single(s => s.Type == "B");
        Assert.Equal(1500, a.Value);
        Assert.Equal(2000, b.Value);
        Assert.Equal(10.0f, a.Returned!.Value, 3);  // 150 / 1500 * 100
        Assert.Equal(0f, b.Returned!.Value, 3);      // no returns for B

        Assert.Equal(3500f, dto.SalesCards.TotalSales.Value, 3);
        Assert.Equal(10.67f, dto.SalesCards.TotalSales.Growth, 2); // (3500-300)/300
        Assert.Equal(150f, dto.SalesCards.ReturnTotal.Value, 3);
        Assert.Equal(2.0f, dto.SalesCards.ReturnTotal.Growth, 2);  // (150-50)/50

        // Legacy never populated these.
        Assert.Null(dto.SalesCards.OffSales);
        Assert.Null(dto.SalesCards.NewModelsSales);
    }

    // ── Categorized customers ─────────────────────────────────────────────────

    [Fact]
    public async Task Categorized_GroupsByCustomerTypeAndMonth()
    {
        await AddSaleAsync(_prodA, _custKhanegi, 100, day: "2026-03-05");
        await AddSaleAsync(_prodA, _custKhanegi, 100, day: "2026-03-20"); // same month + type → count 2
        await AddSaleAsync(_prodA, _custBedehkar, 100, day: "2026-03-08");
        await AddSaleAsync(_prodA, _custKhanegi, 100, day: "2026-04-08"); // different month

        var body = Req("2026-03-01", "2026-04-30", TimeFrameUnit.Month);
        var env = await PostAsync<CategorizedSales>("/api/sales/panel/categorized-customers", body);

        var groups = env.Body!.Sales;
        // Khanegi/March = 2, Bedehkar/March = 1, Khanegi/April = 1
        var marchKhanegi = groups.Single(g =>
            g.Type == CustomerType.MoshtarianKhanegi && g.OnDate.Month == 3);
        Assert.Equal(2, marchKhanegi.Count);
        Assert.Equal(1, groups.Single(g => g.Type == CustomerType.Bedehkaran && g.OnDate.Month == 3).Count);
        Assert.Equal(1, groups.Single(g =>
            g.Type == CustomerType.MoshtarianKhanegi && g.OnDate.Month == 4).Count);
        Assert.All(groups, g => Assert.Equal(1, g.OnDate.Day)); // month bucket start
    }

    // ── Regional (empty parity) ────────────────────────────────────────────────

    [Fact]
    public async Task Regional_ReturnsEmptyPayload_ForParity()
    {
        await AddSaleAsync(_prodA, _custKhanegi, 100, day: "2026-03-05");

        var body = Req("2026-03-01", "2026-03-31");
        var env = await PostAsync<RegionalSalesDto>("/api/sales/panel/regional", body);

        Assert.Equal("success", env.Status);
        Assert.Null(env.Body!.SaleOverRegion);
        Assert.Equal(0, env.Body.TotalSale);
        Assert.Equal(0f, env.Body.GrowthrRate);
    }

    // ── Records (paged/searchable) ─────────────────────────────────────────────

    [Fact]
    public async Task Records_PagesAndReturnsShapedRows()
    {
        for (var i = 0; i < 5; i++)
        {
            await AddSaleAsync(_prodA, _custKhanegi, 100 + i, day: "2026-03-10", docNumber: 1000 + i);
        }

        var body = ReqPaged("2026-03-01", "2026-03-31", pageNumber: 1, pageSize: 2);
        var env = await PostAsync<PagedResult<SalesRecordsDto>>("/api/sales/panel/records", body);

        Assert.Equal("Products fetched successfully", env.Message);
        Assert.Equal(5, env.Body!.TotalCount);
        Assert.Equal(3, env.Body.TotalPages);
        Assert.Equal(2, env.Body.Items.Count);
        var row = env.Body.Items[0];
        Assert.Equal("Ali Ahmadi", row.CustomerName);
        Assert.Equal("Carpet", row.ProductCategory); // ProductType enum → string
        Assert.Equal("Carpet line A", row.ProductDesc);
    }

    [Fact]
    public async Task Records_SearchMatchesCustomerFamilyOrProduct()
    {
        await AddSaleAsync(_prodA, _custKhanegi, 100, day: "2026-03-10");   // Ali Ahmadi
        await AddSaleAsync(_prodB, _custBedehkar, 100, day: "2026-03-11");  // Sara Karimi

        var body = ReqPaged("2026-03-01", "2026-03-31", search: "Karimi");
        var env = await PostAsync<PagedResult<SalesRecordsDto>>("/api/sales/panel/records", body);

        Assert.Equal(1, env.Body!.TotalCount);
        Assert.Equal("Sara Karimi", env.Body.Items[0].CustomerName);
    }

    // ── Top selling ────────────────────────────────────────────────────────────

    [Fact]
    public async Task TopSelling_RanksByDeliveredQuantity_WithGrowth()
    {
        // Current window Mar; previous window Feb (auto).
        await AddSaleAsync(_prodB, _custKhanegi, 2000, day: "2026-03-05", delivered: 10);
        await AddSaleAsync(_prodA, _custKhanegi, 1000, day: "2026-03-06", delivered: 4);
        await AddSaleAsync(_prodA, _custKhanegi, 1000, day: "2026-02-10", delivered: 2); // prev for A

        var body = Req("2026-03-01", "2026-03-31");
        var env = await PostAsync<TopSellingProductsDto>("/api/sales/panel/top-selling", body);

        Assert.Equal("Top products fetched successfully", env.Message);
        var items = env.Body!.Items;
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Rank);
        Assert.Equal("Carpet line B", items[0].ProductName); // 10 delivered → rank 1
        Assert.Equal(10, items[0].Count);
        Assert.Equal(2, items[1].Rank);
        Assert.Equal("Carpet line A", items[1].ProductName); // 4 delivered → rank 2
        Assert.Equal(4, items[1].Count);
        Assert.Equal(1.0f, items[1].Growth, 2); // (4-2)/2 for A
        Assert.Equal(0f, items[0].Growth, 2);   // B had no previous
    }

    [Fact]
    public async Task Panel_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/sales/panel/summary", Req("2026-03-01", "2026-03-31"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static object Req(string start, string end, TimeFrameUnit unit = TimeFrameUnit.Day) => new
    {
        dateFilter = new { startTime = start + "T00:00:00Z", endTime = end + "T23:59:59Z", timeFrameUnit = unit },
        categoryDto = new { productCategory = (string?)null },
        provience = new { provinece = (string?)null },
    };

    private static object ReqPaged(
        string start, string end, int pageNumber = 1, int pageSize = 20, string? search = null) => new
    {
        dateFilter = new { startTime = start + "T00:00:00Z", endTime = end + "T23:59:59Z", timeFrameUnit = TimeFrameUnit.Day },
        categoryDto = new { productCategory = (string?)null },
        provience = new { provinece = (string?)null },
        paggination = new { pageNumber, pageSize },
        searchTerm = search,
    };

    private async Task<ApiResponse<T>> PostAsync<T>(string url, object body)
    {
        var response = await _client.PostAsJsonAsync(url, body);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>(Json))!;
    }

    private async Task AddSaleAsync(
        Guid productId, Guid customerId, long price, string day,
        int docNumber = 1, float delivered = 1f)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        var date = DateTime.SpecifyKind(
            DateTime.Parse(day, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        db.Sales.Add(Sale.Create(_companyId, date, price, delivered, delivered, docNumber, productId, customerId));
        await db.SaveChangesAsync();
    }

    private async Task AddReturnAsync(Guid productId, Guid customerId, long price, string day)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        var date = DateTime.SpecifyKind(
            DateTime.Parse(day, CultureInfo.InvariantCulture), DateTimeKind.Utc);
        db.SalesReturns.Add(SalesReturn.Create(_companyId, date, price, 1f, 1f, 1, productId, customerId));
        await db.SaveChangesAsync();
    }

    private async Task SeedFixtureAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        _companyId = await db.Companies.Select(c => c.Id).FirstAsync();

        var productA = Product.Create(_companyId, ProductType.Carpet, "PROD-A", "Carpet line A", "A");
        var productB = Product.Create(_companyId, ProductType.Carpet, "PROD-B", "Carpet line B", "B");
        var p1 = Person.Create("Ali", "Ahmadi", null, null, "09121111111", null, null, null, null, null);
        var p2 = Person.Create("Sara", "Karimi", null, null, "09122222222", null, null, null, null, null);
        var c1 = Customer.Create(_companyId, CustomerType.MoshtarianKhanegi, true, 0.8f, p1);
        var c2 = Customer.Create(_companyId, CustomerType.Bedehkaran, true, 0.5f, p2);

        db.Products.AddRange(productA, productB);
        db.Customers.AddRange(c1, c2);
        await db.SaveChangesAsync();

        _prodA = productA.Id;
        _prodB = productB.Id;
        _custKhanegi = c1.Id;
        _custBedehkar = c2.Id;
    }

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
