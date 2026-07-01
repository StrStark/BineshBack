using Microsoft.AspNetCore.Authorization;

namespace Binesh.Identity.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        // SuperAdmin bypasses every permission check
        if (context.User.IsInRole(AppRoles.SuperAdmin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var has = context.User.HasClaim(
            PermissionClaims.ClaimType,
            PermissionClaims.ToClaimValue(requirement.Permission));

        if (has)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
