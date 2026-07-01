using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Auth.VerifyOtp;
using Binesh.Identity.Features.Users.ListUsers;
using Binesh.Identity.Features.Users.Shared;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.Users;

public sealed class UsersTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private const string AdminPhone = "+989121111111";

    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync()
    {
        factory.Sms.Clear();
        await ResetSessionsAsync();
        await EnsureUserAsync(AdminPhone, AppRoles.Admin);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── /me ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyProfile_RequiresAuth()
    {
        var response = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_Authenticated_ReturnsOwnProfile()
    {
        await SignInAsync(AdminPhone);

        var response = await _client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(AdminPhone, me!.PhoneNumber);
        Assert.Equal(AppRoles.Admin, me.Role);
    }

    [Fact]
    public async Task UpdateMyProfile_Authenticated_UpdatesFields()
    {
        await SignInAsync(AdminPhone);

        var response = await _client.PutAsJsonAsync("/api/users/me", new
        {
            firstName = "John",
            lastName = "Doe",
            jobTitle = "Sales Manager",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("John", me!.FirstName);
        Assert.Equal("Doe", me.LastName);
        Assert.Equal("Sales Manager", me.JobTitle);
    }

    // ── Admin reads ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListUsers_Unauthenticated_Returns401()
    {
        var response = await _client.GetAsync("/api/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_AsAdmin_Returns200()
    {
        await SignInAsync(AdminPhone);

        var response = await _client.GetAsync("/api/users?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<ListUsersResponse>();
        Assert.NotNull(page);
        Assert.True(page!.TotalCount >= 2);  // SuperAdmin + this Admin
        Assert.Contains(page.Items, u => u.PhoneNumber == AdminPhone);
        Assert.Contains(page.Items, u => u.Role == AppRoles.SuperAdmin);
    }

    [Fact]
    public async Task ListUsers_NoQueryParams_DefaultsToPage1Size20()
    {
        // Regression: minimal API treated `int page` as required if no value
        // was sent → 500. Must default to page=1, pageSize=20.
        await SignInAsync(AdminPhone);

        var response = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<ListUsersResponse>();
        Assert.NotNull(page);
        Assert.Equal(1, page!.Page);
        Assert.Equal(20, page.PageSize);
    }

    // ── SuperAdmin-only mutations ───────────────────────────────────────────

    [Fact]
    public async Task CreateUser_AsAdmin_Returns403()
    {
        await SignInAsync(AdminPhone);

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = "+989125555555",
            firstName = "New",
            lastName = "Admin",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_AsSuperAdmin_CreatesNewAdmin()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        const string newAdminPhone = "+989125555555";

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = newAdminPhone,
            firstName = "Brand",
            lastName = "New",
            jobTitle = "Regional Manager",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(newAdminPhone, created!.PhoneNumber);
        Assert.Equal(AppRoles.Admin, created.Role);
        Assert.False(created.PhoneNumberConfirmed);  // they confirm on first OTP

        // Now they can sign in via the regular OTP flow.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.NotNull(await db.Users.SingleOrDefaultAsync(u => u.PhoneNumber == newAdminPhone));
    }

    [Fact]
    public async Task CreateUser_DuplicatePhone_Returns409()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        var response = await _client.PostAsJsonAsync("/api/users", new
        {
            phoneNumber = AdminPhone,  // already exists
            firstName = "Conflict",
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_AsSuperAdmin_OnAdmin_Succeeds()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);

        var adminId = await GetUserIdByPhoneAsync(AdminPhone);

        var response = await _client.PutAsJsonAsync($"/api/users/{adminId}", new
        {
            firstName = "Updated",
            lastName = "ByBoss",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal("Updated", updated!.FirstName);
    }

    [Fact]
    public async Task UpdateUser_AsSuperAdmin_OnAnotherSuperAdmin_Returns403()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);
        var superAdminId = await GetUserIdByPhoneAsync(BineshApiFactory.SuperAdminPhone);

        var response = await _client.PutAsJsonAsync($"/api/users/{superAdminId}", new
        {
            firstName = "Hijacked",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsAdmin_Returns403()
    {
        await SignInAsync(AdminPhone);
        var anyId = await GetUserIdByPhoneAsync(AdminPhone);

        var response = await _client.DeleteAsync($"/api/users/{anyId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsSuperAdmin_OnAdmin_Succeeds()
    {
        // Create a fresh disposable admin to delete (so we don't break other tests)
        const string disposablePhone = "+989127777777";
        await EnsureUserAsync(disposablePhone, AppRoles.Admin);

        await SignInAsync(BineshApiFactory.SuperAdminPhone);
        var id = await GetUserIdByPhoneAsync(disposablePhone);

        var response = await _client.DeleteAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        Assert.Null(await db.Users.SingleOrDefaultAsync(u => u.Id == id));
    }

    [Fact]
    public async Task DeleteUser_SuperAdminTargetingSuperAdmin_Returns403()
    {
        await SignInAsync(BineshApiFactory.SuperAdminPhone);
        var superAdminId = await GetUserIdByPhoneAsync(BineshApiFactory.SuperAdminPhone);

        var response = await _client.DeleteAsync($"/api/users/{superAdminId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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

    private async Task<Guid> GetUserIdByPhoneAsync(string phone)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        var user = await db.Users.SingleAsync(u => u.PhoneNumber == phone);
        return user.Id;
    }

    private async Task ResetSessionsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.RefreshTokens.ExecuteDeleteAsync();
        await db.Sessions.ExecuteDeleteAsync();
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
