using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Configuration;
using Binesh.Identity.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Binesh.Identity.Bootstrap;

/// <summary>
/// Runs once on startup, BEFORE the HTTP listener accepts requests.
///
/// - Ensures the <c>SuperAdmin</c> and <c>Admin</c> roles exist.
/// - If <c>Seed:SuperAdmin:PhoneNumber</c> is set AND no SuperAdmin exists,
///   creates one with phone confirmed (so they can sign in via OTP immediately).
///
/// Replaces the old code's <c>Task.Run(async () =&gt; ...)</c> fire-and-forget
/// seeder that raced with the first request and swallowed errors.
/// </summary>
internal sealed class IdentityBootstrapService(
    IServiceProvider services,
    IOptions<SeedSettings> seedOptions,
    ILogger<IdentityBootstrapService> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

        await EnsureRolesAsync(roleManager);
        await EnsureSuperAdminAsync(userManager);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureRolesAsync(RoleManager<Role> roleManager)
    {
        foreach (var roleName in new[] { AppRoles.SuperAdmin, AppRoles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new Role(roleName));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Failed to seed role '{roleName}': "
                        + string.Join("; ", result.Errors.Select(e => e.Description)));
                }
                logger.LogInformation("Seeded role {Role}", roleName);
            }
        }
    }

    private async Task EnsureSuperAdminAsync(UserManager<User> userManager)
    {
        var seed = seedOptions.Value.SuperAdmin;
        var rawPhone = seed.PhoneNumber;

        if (string.IsNullOrWhiteSpace(rawPhone))
        {
            logger.LogDebug("No Seed:SuperAdmin:PhoneNumber configured — skipping SuperAdmin seed.");
            return;
        }

        var phone = PhoneNumberNormalizer.Normalize(rawPhone);
        if (phone is null)
        {
            throw new InvalidOperationException(
                $"Seed:SuperAdmin:PhoneNumber '{rawPhone}' is not a valid phone number.");
        }

        // If ANY SuperAdmin exists already, we're done.
        var existing = await userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);
        if (existing.Count > 0)
        {
            logger.LogDebug(
                "SuperAdmin already exists ({Count}). Seed skipped.", existing.Count);
            return;
        }

        var user = new User
        {
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = true,    // pre-confirmed so they can sign in
            FirstName = seed.FirstName,
            LastName = seed.LastName,
        };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed SuperAdmin: "
                + string.Join("; ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(user, AppRoles.SuperAdmin);
        logger.LogWarning(
            "Seeded SuperAdmin {Phone} — use this account to bootstrap other admins.",
            phone);
    }
}
