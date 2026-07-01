using System.Net;
using System.Net.Http.Json;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.Refresh;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Auth;

public sealed class AuthTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string TestPhone = "+989121234567";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetSessionsAsync();
        await EnsureUserAsync(TestPhone, AppRoles.Admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Closed registration ─────────────────────────────────────────────────

    [Fact]
    public async Task RequestOtp_UnknownPhone_Returns200ButNoSmsSent()
    {
        const string unknown = "+989120000000";

        var response = await _client.PostAsJsonAsync("/api/auth/otp/request", new
        {
            phoneNumber = unknown,
        });

        // 200 to prevent user enumeration, but no SMS goes out.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(factory.Sms.GetLastOtp(unknown));

        // No user was created either — registration is closed.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.Users.SingleOrDefaultAsync(u => u.PhoneNumber == unknown));
    }

    [Fact]
    public async Task RequestOtp_PreSeededUser_SendsSms()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/otp/request", new
        {
            phoneNumber = "09121234567",  // local format, will normalize to TestPhone
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var otp = factory.Sms.GetLastOtp(TestPhone);
        Assert.NotNull(otp);
        Assert.Matches(@"^\d{6}$", otp);
    }

    [Fact]
    public async Task RequestOtp_InvalidPhone_Returns422()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/otp/request", new
        {
            phoneNumber = "12345",
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    // ── Verify OTP ──────────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyOtp_CorrectCode_ReturnsAccessAndRefreshTokens()
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = TestPhone });
        var otp = factory.Sms.GetLastOtp(TestPhone)!;

        var response = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = TestPhone,
            otp,
            deviceInfo = "xunit",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var tokens = await response.Content.ReadFromJsonAsync<VerifyOtpResponse>();
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));

        // Refresh-token expiry sanity: days, not years.
        var refreshDays = (tokens.RefreshTokenExpiresAt - DateTime.UtcNow).TotalDays;
        Assert.InRange(refreshDays, 1, 60);
    }

    [Fact]
    public async Task VerifyOtp_WrongCode_Returns401()
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = TestPhone });

        var response = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = TestPhone,
            otp = "000000",
            deviceInfo = "xunit",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_MagicBackdoorCode696969_Returns401()
    {
        // Regression: the old code accepted "696969" as a master OTP.
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = TestPhone });

        var response = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = TestPhone,
            otp = "696969",
            deviceInfo = "xunit",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyOtp_UnknownPhone_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = "+989120000000",
            otp = "123456",
            deviceInfo = "xunit",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_Valid_RotatesTokens()
    {
        var initial = await SignInAsync();

        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initial.RefreshToken,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var rotated = await response.Content.ReadFromJsonAsync<RefreshResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(initial.RefreshToken, rotated!.RefreshToken);
        Assert.NotEqual(initial.AccessToken, rotated.AccessToken);
    }

    [Fact]
    public async Task Refresh_ReuseOfAlreadyUsedToken_RevokesSessionAndReturns401()
    {
        var initial = await SignInAsync();

        var firstUse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initial.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, firstUse.StatusCode);

        var secondUse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initial.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, secondUse.StatusCode);

        var rotated = await firstUse.Content.ReadFromJsonAsync<RefreshResponse>();
        var thirdUse = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = rotated!.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, thirdUse.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = "definitely-not-a-real-token-just-some-padding-to-pass-length-validator",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── SignOut ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SignOut_Valid_RevokesSession()
    {
        var initial = await SignInAsync();

        var signOut = await _client.PostAsJsonAsync("/api/auth/signout", new
        {
            refreshToken = initial.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.OK, signOut.StatusCode);

        var afterSignOut = await _client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = initial.RefreshToken,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<VerifyOtpResponse> SignInAsync()
    {
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phoneNumber = TestPhone });
        var otp = factory.Sms.GetLastOtp(TestPhone)!;
        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new
        {
            phoneNumber = TestPhone,
            otp,
            deviceInfo = "xunit",
        });
        verify.EnsureSuccessStatusCode();
        return (await verify.Content.ReadFromJsonAsync<VerifyOtpResponse>())!;
    }

    private async Task ResetSessionsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();

        // Reset OTP request gating so each test starts clean.
        await db.Users.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastOtpRequestedAt, (DateTimeOffset?)null));
    }

    private async Task EnsureUserAsync(string phone, string role)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.SingleOrDefaultAsync(u => u.PhoneNumber == phone);
        if (user is null)
        {
            user = new User
            {
                UserName = phone,
                PhoneNumber = phone,
                PhoneNumberConfirmed = true,
            };
            await userManager.CreateAsync(user);
        }
        var roles = await userManager.GetRolesAsync(user);
        if (!roles.Contains(role))
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
