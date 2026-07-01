using Binesh.Ai.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Binesh.Api.HealthChecks;

/// <summary>
/// Cheap readiness check — only verifies the AI client is configured.
/// We deliberately do NOT ping OpenAI on every health check because:
///   1. /readyz is hit by orchestrators on a 5-30s interval (cost),
///   2. an OpenAI outage doesn't mean the API is unhealthy (degraded, yes; unhealthy, no).
/// </summary>
public sealed class OpenAiHealthCheck(IOptions<OpenAiSettings> settings) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var value = settings.Value;

        if (string.IsNullOrWhiteSpace(value.ApiKey))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("OPENAI_API_KEY is not configured"));
        }

        if (string.IsNullOrWhiteSpace(value.BaseUrl) ||
            !Uri.TryCreate(value.BaseUrl, UriKind.Absolute, out _))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"OPENAI_BASE_URL is invalid: '{value.BaseUrl}'"));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"OpenAI client configured for {value.BaseUrl} (model: {value.Model})"));
    }
}
