using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Application.Abstractions;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Identity.Features.Users.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Users;

/// <summary>
/// Round 15 — POST upload-url → PUT object-key → GET /me embeds a fresh
/// pre-signed download URL. The <see cref="InMemoryFileStorage"/> fake
/// records every storage call so tests can assert on object-key layout +
/// cross-user namespace enforcement.
/// </summary>
public sealed class ProfileImageTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AphonePhone = "+989121212121";
    private const string BphonePhone = "+989121212122";
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        factory.FileStorage.Reset();
        await ResetAsync();
        await EnsureUserAsync(AphonePhone, AppRoles.Admin);
        await EnsureUserAsync(BphonePhone, AppRoles.Admin);
        await SignInAsync(AphonePhone);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RequestUploadUrl_ReturnsUserScopedKey()
    {
        var meId = await CurrentUserIdAsync();

        var response = await _client.PostAsJsonAsync(
            "/api/users/me/profile-image/upload-url", new { contentType = "image/jpeg" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PresignedUploadUrl>();

        Assert.StartsWith($"profile-images/{meId:N}/", body!.ObjectKey);
        Assert.EndsWith(".jpg", body.ObjectKey);
        Assert.Equal("PUT", body.Method);
        Assert.Equal("image/jpeg", body.RequiredHeaders["Content-Type"]);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5));
    }

    [Fact]
    public async Task RequestUploadUrl_RejectsUnsupportedContentType()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/users/me/profile-image/upload-url", new { contentType = "application/pdf" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetProfileImage_WithoutUpload_Returns422()
    {
        var meId = await CurrentUserIdAsync();
        var key = $"profile-images/{meId:N}/never-uploaded.jpg";

        var response = await _client.PutAsJsonAsync("/api/users/me/profile-image", new { objectKey = key });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task SetProfileImage_WithDifferentUsersKey_Returns422()
    {
        var meId = await CurrentUserIdAsync();
        var otherId = await GetUserIdByPhoneAsync(BphonePhone);
        var foreignKey = $"profile-images/{otherId:N}/img.jpg";
        factory.FileStorage.MarkUploaded(foreignKey);

        var response = await _client.PutAsJsonAsync(
            "/api/users/me/profile-image", new { objectKey = foreignKey });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task FullRoundTrip_RequestUpload_Set_GetMeEmbedsDownloadUrl()
    {
        // Request upload URL
        var upload = await (await _client.PostAsJsonAsync(
            "/api/users/me/profile-image/upload-url", new { contentType = "image/png" }))
            .Content.ReadFromJsonAsync<PresignedUploadUrl>();
        Assert.NotNull(upload);

        // Simulate the client completing the upload
        factory.FileStorage.MarkUploaded(upload!.ObjectKey);

        // Set
        var set = await _client.PutAsJsonAsync(
            "/api/users/me/profile-image", new { objectKey = upload.ObjectKey });
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var updated = await set.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(upload.ObjectKey, updated!.ProfileImageName);

        // GET /me embeds a fresh pre-signed download URL
        var me = await (await _client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(upload.ObjectKey, me!.ProfileImageName);
        Assert.NotNull(me.ProfileImageUrl);
        Assert.Contains(Uri.EscapeDataString(upload.ObjectKey), me.ProfileImageUrl);
        Assert.Contains(upload.ObjectKey, factory.FileStorage.DownloadUrlRequests);
    }

    [Fact]
    public async Task SetProfileImage_ReplacesPriorAndDeletesOld()
    {
        // First upload+set
        var first = await UploadAndSetAsync("image/jpeg");
        Assert.Contains(first, factory.FileStorage.UploadUrlRequests);

        // Second upload+set
        var second = await UploadAndSetAsync("image/jpeg");
        Assert.NotEqual(first, second);
        Assert.Contains(first, factory.FileStorage.DeletedKeys);
    }

    [Fact]
    public async Task ClearProfileImage_NullsTheKey_AndDeletesObject()
    {
        var key = await UploadAndSetAsync("image/jpeg");

        var clear = await _client.DeleteAsync("/api/users/me/profile-image");
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var dto = await clear.Content.ReadFromJsonAsync<UserDto>();
        Assert.Null(dto!.ProfileImageName);
        Assert.Null(dto.ProfileImageUrl);
        Assert.Contains(key, factory.FileStorage.DeletedKeys);
    }

    [Fact]
    public async Task GetMe_NoImage_LeavesProfileImageUrlNull()
    {
        var me = await (await _client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<UserDto>();
        Assert.Null(me!.ProfileImageName);
        Assert.Null(me.ProfileImageUrl);
        Assert.Empty(factory.FileStorage.DownloadUrlRequests);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync(
            "/api/users/me/profile-image/upload-url", new { contentType = "image/jpeg" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string> UploadAndSetAsync(string contentType)
    {
        var upload = await (await _client.PostAsJsonAsync(
            "/api/users/me/profile-image/upload-url", new { contentType }))
            .Content.ReadFromJsonAsync<PresignedUploadUrl>();
        factory.FileStorage.MarkUploaded(upload!.ObjectKey);
        var set = await _client.PutAsJsonAsync(
            "/api/users/me/profile-image", new { objectKey = upload.ObjectKey });
        set.EnsureSuccessStatusCode();
        return upload.ObjectKey;
    }

    private async Task<Guid> CurrentUserIdAsync()
    {
        var me = await (await _client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<UserDto>();
        return me!.Id;
    }

    private async Task<Guid> GetUserIdByPhoneAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.Users.SingleAsync(u => u.PhoneNumber == phone);
        return user.Id;
    }

    private async Task SignInAsync(string phone)
    {
        _client.DefaultRequestHeaders.Authorization = null;
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
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var db = scope.ServiceProvider.GetRequiredService<Binesh.Infrastructure.Persistence.BineshDbContext>();
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
        sa.ProfileImageName = null;
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
