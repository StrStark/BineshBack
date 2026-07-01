using Microsoft.AspNetCore.Authorization;

namespace Binesh.Identity.Authorization;

/// <summary>
/// Marker used by policies of the form "permission:UserManagement".
/// The handler verifies the caller has the matching claim.
///
/// Replaces the broken RequirePermissionAttribute from the old code
/// (which tried to inject AppPermission via DI into an attribute — impossible).
/// </summary>
public sealed class PermissionRequirement(AppPermission permission) : IAuthorizationRequirement
{
    public AppPermission Permission { get; } = permission;
}
