using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Features.Financial.GetFinancialPanel;
using Binesh.Application.Features.Financial.ListFinancialEntries;
using Binesh.Application.Features.Financial.Shared;
using Binesh.Domain.Financial;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Financial;

/// <summary>
/// Round 11 — integration tests for FinancialEntry CRUD, mapping settings
/// upsert, and the combined panel endpoint.
///
/// Panel assertions check the legacy formulas verbatim (including the four
/// known bugs documented in CHANGES.md). When the cleanup pass lands later
/// these tests will move to assert the corrected math instead.
/// </summary>
public sealed class FinancialTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989126666666";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
        await SignInAsync(AdminPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Entries CRUD ────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateEntry_HappyPath_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/financial/entries", new
        {
            code = "1001", name = "Cash", type = "Asset", debit = 500_000L, credit = 0L,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<FinancialEntryDto>();
        Assert.Equal("1001", dto!.Code);
        Assert.Equal("Cash", dto.Name);
        Assert.Equal(500_000L, dto.Debit);
    }

    [Fact]
    public async Task CreateEntry_EmptyCode_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/financial/entries", new
        {
            code = "", name = "x", type = "Asset", debit = 0L, credit = 0L,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateEntry_NegativeDebit_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/financial/entries", new
        {
            code = "1", name = "x", type = "Asset", debit = -1L, credit = 0L,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task GetEntryById_Existing_Returns200()
    {
        var created = await CreateAsync("1002", "Bank", "Asset", debit: 1000);
        var response = await _client.GetAsync($"/api/financial/entries/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<FinancialEntryDto>();
        Assert.Equal("Bank", dto!.Name);
    }

    [Fact]
    public async Task GetEntryById_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/financial/entries/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListEntries_FiltersByTypeAndSearch()
    {
        await CreateAsync("1001", "Cash", "Asset");
        await CreateAsync("1002", "Bank", "Asset");
        await CreateAsync("2001", "Loans", "Liability");

        var byType = await _client.GetAsync("/api/financial/entries?type=Asset");
        var typePage = await byType.Content.ReadFromJsonAsync<ListFinancialEntriesResponse>();
        Assert.Equal(2, typePage!.Items.Count);
        Assert.All(typePage.Items, e => Assert.Equal("Asset", e.Type));

        var bySearch = await _client.GetAsync("/api/financial/entries?search=bank");
        var searchPage = await bySearch.Content.ReadFromJsonAsync<ListFinancialEntriesResponse>();
        Assert.Single(searchPage!.Items);
        Assert.Equal("Bank", searchPage.Items[0].Name);
    }

    [Fact]
    public async Task UpdateEntry_PartialPatch_OnlyChangesSetFields()
    {
        var created = await CreateAsync("1001", "Cash", "Asset", debit: 100, credit: 0);
        var response = await _client.PutAsJsonAsync($"/api/financial/entries/{created.Id}", new
        {
            credit = 50L,  // debit untouched
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<FinancialEntryDto>();
        Assert.Equal(100L, dto!.Debit);
        Assert.Equal(50L, dto.Credit);
    }

    [Fact]
    public async Task DeleteEntry_Existing_Returns204()
    {
        var created = await CreateAsync("1099", "Tmp", "Asset");
        var response = await _client.DeleteAsync($"/api/financial/entries/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var followup = await _client.GetAsync($"/api/financial/entries/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, followup.StatusCode);
    }

    // ── Mapping settings ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSettings_NoneConfigured_Returns404()
    {
        var response = await _client.GetAsync("/api/financial/mapping-settings");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpsertSettings_CreatesThenReplaces()
    {
        var first = await _client.PutAsJsonAsync("/api/financial/mapping-settings", new
        {
            toCalculateSales = new[] { new { title = "Sales", value = (long?)null } },
            operationalCost = new[] { new { title = "Rent", value = (long?)null } },
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstDto = await first.Content.ReadFromJsonAsync<MappingSettingsDto>();
        Assert.Single(firstDto!.ToCalculateSales);
        Assert.Equal("Sales", firstDto.ToCalculateSales[0].Title);

        // Second PUT replaces (must NOT create a duplicate row)
        var second = await _client.PutAsJsonAsync("/api/financial/mapping-settings", new
        {
            toCalculateSales = new[]
            {
                new { title = "Sales", value = (long?)null },
                new { title = "OtherSales", value = (long?)null },
            },
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var secondDto = await second.Content.ReadFromJsonAsync<MappingSettingsDto>();
        Assert.Equal(firstDto.Id, secondDto!.Id);
        Assert.Equal(2, secondDto.ToCalculateSales.Count);
        Assert.Empty(secondDto.OperationalCost);  // omitted in second body → empty

        // GET returns the singleton
        var get = await _client.GetAsync("/api/financial/mapping-settings");
        var getDto = await get.Content.ReadFromJsonAsync<MappingSettingsDto>();
        Assert.Equal(secondDto.Id, getDto!.Id);
    }

    // ── Panel ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Panel_NoSettings_Returns404()
    {
        var response = await _client.GetAsync("/api/financial/panel");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Panel_ComputesLegacyFormulas()
    {
        // Seed mapping + accounts using the DbContext directly to keep the
        // test focused on the math.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var settings = FinancialMappingSettings.Create(
            toCalculateSales:        [new DetailedItem("Sales", null)],
            operationalCost:         [new DetailedItem("Rent", null)],
            payables:                [new DetailedItem("Payable", null)],
            toCalculateLiquidity:    [new DetailedItem("Cash", null)],
            toCalculateGrossProfitLoss:    [new DetailedItem("Sales", null)],
            toCalculateOperatingProfitLoss: [new DetailedItem("Rent", null)],
            toCalculateProfitLossBeforTax:  [new DetailedItem("Tax", null)],
            toCalculateNetProfitLoss:       [new DetailedItem("Net", null)],
            toCalculateAccumulatedProfitLoss: [new DetailedItem("Acc", null)],
            toCalculateEquity:       [new DetailedItem("Equity", null)]);

        db.FinancialMappingSettings.Add(settings);
        db.FinancialEntries.AddRange(
            FinancialEntry.Create("1", "Sales", "Revenue", debit: 0, credit: 10_000),
            FinancialEntry.Create("2", "Rent", "Expense", debit: 2_000, credit: 0),
            FinancialEntry.Create("3", "Payable", "Liability", debit: 0, credit: 500),
            FinancialEntry.Create("4", "Cash", "Asset", debit: 800, credit: 0),
            FinancialEntry.Create("5", "Tax", "Expense", debit: 100, credit: 0),
            FinancialEntry.Create("6", "Net", "Other", debit: 0, credit: 50),
            FinancialEntry.Create("7", "Acc", "Other", debit: 0, credit: 20),
            FinancialEntry.Create("8", "Equity", "Equity", debit: 0, credit: 5_000));
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/financial/panel");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<GetFinancialPanelResponse>();
        Assert.NotNull(body);

        // SumFor(mapping) = sum(Credit - Debit) over matching account names.
        var totalSale = 10_000L;                            // Sales: 10000-0
        var operationalCosts = -2_000L;                     // Rent: 0-2000
        var payable = 500L;                                 // Payable: 500-0
        var liquidity = -800L;                              // Cash: 0-800
        var grossPL = 10_000L;                              // Sales
        var operatingPL = -2_000L + grossPL;                // Rent (+grossPL)
        var beforTax = -100L + operationalCosts;            // Tax (+operationalCosts — PARITY-BUG #2)
        var netPL = 50L + beforTax;                         // Net (+beforTax)
        var accumulated = 20L + netPL;                      // Acc (+netPL)
        var equity = 5_000L;

        // PARITY-BUG #1 — operator precedence preserved:
        //   totalSale - ((operationalCosts + payable) / (double)totalSale)
        var expectedProfitMargin = totalSale - ((double)(operationalCosts + payable) / totalSale);

        Assert.Equal(totalSale, body!.StateCards.TotalSale.Value);
        Assert.Equal(liquidity, body.StateCards.Liquidity.Value);
        Assert.Equal(netPL, body.StateCards.NetProfit.Value);
        Assert.Equal(expectedProfitMargin, body.StateCards.ProfitMargin.Value, 3);

        Assert.Equal(grossPL, body.ProfitLoss.GrossProfitLoss.Value.Value);
        Assert.Equal(operatingPL, body.ProfitLoss.OperationalProfitLoss.Value.Value);
        Assert.Equal(beforTax, body.ProfitLoss.ProfitLossBeforTax.Value.Value);
        Assert.Equal(netPL, body.ProfitLoss.NetProfitLoss.Value.Value);
        Assert.Equal(accumulated, body.ProfitLoss.AccumulatedProfitLoss.Value.Value);

        Assert.Equal(equity, body.BalanceSheet.StateCards.Equities.Value);
        // PARITY-BUG #3: Assets == Liability (both filter Value>0)
        Assert.Equal(body.BalanceSheet.StateCards.Assets.Value,
                     body.BalanceSheet.StateCards.Liability.Value);

        // BalanceSheet items grouped by account Type
        var types = body.BalanceSheet.Items.MainItems.Select(m => m.Title).ToHashSet();
        Assert.Contains("Asset", types);
        Assert.Contains("Liability", types);
        Assert.Contains("Equity", types);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/api/financial/entries");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<FinancialEntryDto> CreateAsync(string code, string name, string type, long debit = 0, long credit = 0)
    {
        var response = await _client.PostAsJsonAsync("/api/financial/entries", new
        {
            code, name, type, debit, credit,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FinancialEntryDto>())!;
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
        await db.FinancialEntries.ExecuteDeleteAsync();
        await db.FinancialMappingSettings.ExecuteDeleteAsync();

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
