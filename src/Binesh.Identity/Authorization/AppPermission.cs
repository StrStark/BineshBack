namespace Binesh.Identity.Authorization;

/// <summary>
/// The 8 permission toggles shown in the access management modal.
/// Each maps to one ASP.NET Identity user claim of type "permission".
/// </summary>
public enum AppPermission
{
    Dashboard = 0,
    ContactInfo = 1,
    Specialists = 2,
    CustomerInfo = 3,
    Reporting = 4,
    Analytics = 5,
    UserManagement = 6,
    Settings = 7,
}

public static class PermissionClaims
{
    public const string ClaimType = "permission";

    public static string ToClaimValue(AppPermission permission) => permission.ToString();

    public static IEnumerable<AppPermission> All => Enum.GetValues<AppPermission>();
}

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [SuperAdmin, Admin, User];
}
