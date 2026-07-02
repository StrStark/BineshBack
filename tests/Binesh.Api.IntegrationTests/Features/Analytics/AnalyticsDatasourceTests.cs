using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Analytics;

public sealed class AnalyticsDatasourceTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetStateAsync();
        await SignInAsync(BineshApiFactory.SuperAdminPhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ListDataSources_ReturnsFourSelectableBiSources()
    {
        var response = await _client.GetAsync("/api/data-sources");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = ReadEnvelopeBody(await response.Content.ReadAsStringAsync());
        var ids = body.EnumerateArray()
            .Select(item => item.GetProperty("id").GetString())
            .ToList();

        Assert.Equal(new[] { "Sale", "Product", "Customer", "Financial" }, ids);
        foreach (var item in body.EnumerateArray())
        {
            Assert.Equal("SqlServer", item.GetProperty("provider").GetString());
            Assert.True(item.GetProperty("enabled").GetBoolean());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("label").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("description").GetString()));
            Assert.True(item.GetProperty("dimensionCount").GetInt32() > 0);
        }
    }

    [Fact]
    public async Task GetDataSource_ReturnsFieldsRolesAggregationsAndFilterOperators()
    {
        var response = await _client.GetAsync("/api/data-sources/Sale");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = ReadEnvelopeBody(await response.Content.ReadAsStringAsync());
        Assert.Equal("Sale", body.GetProperty("id").GetString());

        var fields = body.GetProperty("fields")
            .EnumerateArray()
            .ToDictionary(f => f.GetProperty("name").GetString()!, f => f);

        Assert.Equal("measure", fields["LinePrice"].GetProperty("role").GetString());
        Assert.Contains("sum", fields["LinePrice"].GetProperty("allowedAggregations").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("dimension", fields["CustomerName"].GetProperty("role").GetString());
        Assert.Contains("contains", fields["CustomerName"].GetProperty("filterOperators").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("dimension", fields["YearID"].GetProperty("role").GetString());
    }

    [Fact]
    public async Task GetDataSource_InvalidId_Returns404()
    {
        var response = await _client.GetAsync("/api/data-sources/UnknownDataset");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DbQuery_TableCompatibility_UsesTableAsDatasetId()
    {
        var response = await _client.PostAsJsonAsync("/api/db-query", new
        {
            table = "Sale",
            values = new[]
            {
                new { field = "doesNotExist", aggregation = "sum", alias = "broken" },
            },
            limit = 5,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("doesNotExist", json);
    }

    [Fact]
    public async Task AnalyticsQuery_SourceAsDatasourceId_UsesSourceAsDatasetId()
    {
        var response = await _client.PostAsJsonAsync("/api/analytics/query", new
        {
            sourceId = "Financial",
            values = new[]
            {
                new { field = "doesNotExist", aggregation = "sum", alias = "broken" },
            },
            limit = 5,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("doesNotExist", json);
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

    private async Task ResetStateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastOtpRequestedAt, (DateTimeOffset?)null));
    }

    private static JsonElement ReadEnvelopeBody(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("body").Clone();
    }
}
