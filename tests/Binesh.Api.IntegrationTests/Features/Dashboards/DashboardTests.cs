using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Dashboards;

public sealed class DashboardTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetStateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task DashboardCrud_StoresConfigJsonWithoutExecutingSql()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        var create = await _client.PostAsJsonAsync("/api/dashboards", new
        {
            name = "Sales overview",
            description = "BI dashboard",
            icon = "BarChart",
            config = ValidConfig(),
        });

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = ReadEnvelopeBody(await create.Content.ReadAsStringAsync());
        var id = created.GetProperty("id").GetGuid();
        Assert.Equal("Sales overview", created.GetProperty("name").GetString());
        Assert.Equal("Sale", created.GetProperty("config").GetProperty("widgets")[0].GetProperty("query").GetProperty("datasetId").GetString());

        var list = await _client.GetAsync("/api/dashboards");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var items = ReadEnvelopeBody(await list.Content.ReadAsStringAsync());
        Assert.Contains(items.EnumerateArray(), item => item.GetProperty("id").GetGuid() == id);

        var update = await _client.PutAsJsonAsync($"/api/dashboards/{id}", new
        {
            name = "Updated sales overview",
            config = ValidConfig(limit: 10),
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = ReadEnvelopeBody(await update.Content.ReadAsStringAsync());
        Assert.Equal("Updated sales overview", updated.GetProperty("name").GetString());
        Assert.Equal(10, updated.GetProperty("config").GetProperty("widgets")[0].GetProperty("query").GetProperty("limit").GetInt32());

        var delete = await _client.DeleteAsync($"/api/dashboards/{id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    [Fact]
    public async Task DashboardCreate_InvalidDatasetField_Returns409()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        var response = await _client.PostAsJsonAsync("/api/dashboards", new
        {
            name = "Broken dashboard",
            config = new
            {
                widgets = new[]
                {
                    new
                    {
                        id = "broken",
                        query = new
                        {
                            sourceId = "default-sqlserver",
                            datasetId = "Sale",
                            labelField = "Date",
                            valueField = "doesNotExist",
                            aggregation = "sum",
                        },
                    },
                },
            },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static object ValidConfig(int limit = 5) => new
    {
        version = 1,
        widgets = new[]
        {
            new
            {
                id = "sales-by-date",
                type = "line",
                title = "Sales by date",
                query = new
                {
                    sourceId = "default-sqlserver",
                    datasetId = "Sale",
                    labelField = "Date",
                    valueField = "LinePrice",
                    aggregation = "sum",
                    limit,
                },
            },
        },
    };

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
        await db.Dashboards.ExecuteDeleteAsync();
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
