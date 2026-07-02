using System.Security.Claims;
using Binesh.Application.Abstractions;
using Binesh.Domain.Ai;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Api.Endpoints.Ai;

public static class AiSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAiSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/users/me/ai-settings", GetSettings)
            .WithTags("AiSettings")
            .RequireAuthorization()
            .WithName("GetUserAiSettings");

        routes.MapPut("/api/users/me/ai-settings", UpsertSettings)
            .WithTags("AiSettings")
            .RequireAuthorization()
            .WithName("UpsertUserAiSettings");

        // Compatibility with the current Next route.
        routes.MapGet("/api/ai-settings", GetSettings)
            .WithTags("AiSettings")
            .RequireAuthorization()
            .WithName("GetAiSettingsCompatibility");

        routes.MapPut("/api/ai-settings", UpsertSettings)
            .WithTags("AiSettings")
            .RequireAuthorization()
            .WithName("UpsertAiSettingsCompatibility");

        return routes;
    }

    private static async Task<IResult> GetSettings(
        ClaimsPrincipal user,
        IBineshDbContext db,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        var settings = await db.UserAiSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);

        return Results.Ok(ApiEnvelope.Success(new
        {
            apiKeyConfigured = !string.IsNullOrWhiteSpace(settings?.ApiKeyEncrypted),
            apiKeyPreview = settings?.ApiKeyPreview,
            model = settings?.Model,
            baseUrl = settings?.BaseUrl,
            apiUrl = settings?.BaseUrl,
        }));
    }

    private static async Task<IResult> UpsertSettings(
        [FromBody] AiSettingsRequest body,
        ClaimsPrincipal user,
        IBineshDbContext db,
        IAiSettingsProtector protector,
        CancellationToken ct)
    {
        var userId = RequireUserId(user);
        var settings = await db.UserAiSettings
            .SingleOrDefaultAsync(s => s.UserId == userId, ct);

        if (settings is null)
        {
            settings = new UserAiSettings { UserId = userId };
            db.UserAiSettings.Add(settings);
        }

        if (body.ApiKey is not null)
        {
            if (string.IsNullOrWhiteSpace(body.ApiKey))
            {
                settings.ApiKeyEncrypted = null;
                settings.ApiKeyPreview = null;
            }
            else
            {
                var trimmed = body.ApiKey.Trim();
                settings.ApiKeyEncrypted = protector.Protect(trimmed);
                settings.ApiKeyPreview = trimmed.Length <= 8
                    ? "********"
                    : $"{trimmed[..4]}...{trimmed[^4..]}";
            }
        }

        if (body.Model is not null)
        {
            settings.Model = string.IsNullOrWhiteSpace(body.Model) ? null : body.Model.Trim();
        }

        var baseUrl = body.BaseUrl ?? body.ApiUrl;
        if (baseUrl is not null)
        {
            settings.BaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.Trim().TrimEnd('/');
        }

        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(ApiEnvelope.Success(new
        {
            apiKeyConfigured = !string.IsNullOrWhiteSpace(settings.ApiKeyEncrypted),
            apiKeyPreview = settings.ApiKeyPreview,
            model = settings.Model,
            baseUrl = settings.BaseUrl,
            apiUrl = settings.BaseUrl,
        }));
    }

    private static Guid RequireUserId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Authenticated user has no NameIdentifier claim."));

    public sealed record AiSettingsRequest(
        string? ApiKey,
        string? Model,
        string? BaseUrl,
        string? ApiUrl);
}
