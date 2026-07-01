using System.ComponentModel.DataAnnotations;

namespace Binesh.Identity.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required] public string Issuer { get; set; } = "binesh.api";
    [Required] public string Audience { get; set; } = "binesh.web";

    /// <summary>
    /// Path to the signing certificate (.pfx). Optional at startup — cert is loaded lazily
    /// on first auth attempt. Missing in prod = first auth request fails with a clear error.
    /// </summary>
    public string? SigningCertificatePath { get; set; }

    /// <summary>Password for the signing certificate. MUST come from env var in prod, never a settings file.</summary>
    public string? SigningCertificatePassword { get; set; }

    /// <summary>Access-token lifetime. Default 15 minutes. Format: "D.HH:mm:ss".</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Refresh-token lifetime. Default 14 days.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(14);
}
