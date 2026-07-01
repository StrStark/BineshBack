namespace Binesh.Api.Configuration;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    public PolicyConfig Auth { get; set; } = new() { PermitLimit = 5, WindowSeconds = 60 };
    public PolicyConfig Ai { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public PolicyConfig Default { get; set; } = new() { PermitLimit = 100, WindowSeconds = 60 };

    public sealed class PolicyConfig
    {
        public int PermitLimit { get; set; }
        public int WindowSeconds { get; set; }
    }
}
