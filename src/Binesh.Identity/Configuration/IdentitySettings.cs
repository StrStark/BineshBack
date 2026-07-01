namespace Binesh.Identity.Configuration;

/// <summary>
/// Password / lockout / sign-in policy settings. Bound from <c>Identity:*</c>.
/// All defaults are conservative; override per environment via env vars.
/// </summary>
public sealed class IdentitySettings
{
    public const string SectionName = "Identity";

    public PasswordPolicy Password { get; set; } = new();
    public LockoutPolicy Lockout { get; set; } = new();
    public OtpPolicy Otp { get; set; } = new();

    public sealed class PasswordPolicy
    {
        public int RequiredLength { get; set; } = 8;
        public bool RequireDigit { get; set; } = true;
        public bool RequireLowercase { get; set; } = false;
        public bool RequireUppercase { get; set; } = false;
        public bool RequireNonAlphanumeric { get; set; } = false;
    }

    public sealed class LockoutPolicy
    {
        public int MaxFailedAttempts { get; set; } = 5;
        public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);
    }

    public sealed class OtpPolicy
    {
        /// <summary>Minimum gap between two OTP requests for the same phone.</summary>
        public TimeSpan ResendDelay { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>How long an issued OTP remains valid.</summary>
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5);
    }
}
