using System.ComponentModel.DataAnnotations;

namespace Binesh.Ai.Configuration;

/// <summary>
/// OpenAI client config. BaseUrl is settable so any OpenAI-compatible
/// gateway can be used (api.openai.com, api.gapgpt.ir, Azure OpenAI,
/// self-hosted vLLM, etc.) without code changes.
/// </summary>
public sealed class OpenAiSettings
{
    public const string SectionName = "OpenAI";

    [Required]
    public string ApiKey { get; set; } = default!;

    /// <summary>OpenAI-compatible endpoint. Default: https://api.openai.com/v1</summary>
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    /// <summary>Primary model used for chat completions.</summary>
    [Required]
    public string Model { get; set; } = "gpt-4o";

    /// <summary>Used when the primary model returns rate-limit or quota errors.</summary>
    public string? FallbackModel { get; set; }

    /// <summary>HTTP timeout for OpenAI requests.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Daily per-user token budget enforced by <c>ITokenBudget</c>. The
    /// orchestrator returns 429 before invoking OpenAI once the user has
    /// consumed this many tokens within a 24-hour rolling window. Set to 0
    /// to disable the budget (not recommended for production).
    /// </summary>
    public int PerUserDailyTokens { get; set; } = 100_000;
}
