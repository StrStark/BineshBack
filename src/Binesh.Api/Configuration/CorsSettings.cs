namespace Binesh.Api.Configuration;

public sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Exact origin URLs allowed to call the API. No wildcards.</summary>
    public string[] AllowedOrigins { get; set; } = [];
}
