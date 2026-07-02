using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Ai;

public sealed class AiSettingsTests(BineshApiFactory factory)
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
    public async Task UserAiSettings_SaveReadAndClear_MasksKeyAndNeverReturnsRawSecret()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        var save = await _client.PutAsJsonAsync("/api/users/me/ai-settings", new
        {
            apiKey = "sk-test-secret-123456",
            model = "gpt-4o-mini",
            baseUrl = "https://provider.example/v1",
        });

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saveText = await save.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sk-test-secret-123456", saveText);
        var saved = ReadEnvelopeBody(saveText);
        Assert.True(saved.GetProperty("apiKeyConfigured").GetBoolean());
        Assert.Equal("sk-t...3456", saved.GetProperty("apiKeyPreview").GetString());
        Assert.Equal("gpt-4o-mini", saved.GetProperty("model").GetString());
        Assert.Equal("https://provider.example/v1", saved.GetProperty("baseUrl").GetString());

        var get = await _client.GetAsync("/api/users/me/ai-settings");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var getText = await get.Content.ReadAsStringAsync();
        Assert.DoesNotContain("sk-test-secret-123456", getText);
        var current = ReadEnvelopeBody(getText);
        Assert.True(current.GetProperty("apiKeyConfigured").GetBoolean());
        Assert.Equal("sk-t...3456", current.GetProperty("apiKeyPreview").GetString());

        var clear = await _client.PutAsJsonAsync("/api/users/me/ai-settings", new
        {
            apiKey = "",
        });

        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var cleared = ReadEnvelopeBody(await clear.Content.ReadAsStringAsync());
        Assert.False(cleared.GetProperty("apiKeyConfigured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("apiKeyPreview").ValueKind);
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
        await db.UserAiSettings.ExecuteDeleteAsync();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastOtpRequestedAt, (DateTimeOffset?)null));

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var superAdmin = await userManager.Users.SingleAsync(u => u.PhoneNumber == BineshApiFactory.SuperAdminPhone);
        superAdmin.PhoneNumberConfirmed = true;
        await userManager.UpdateAsync(superAdmin);
    }

    private static JsonElement ReadEnvelopeBody(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("body").Clone();
    }
}
