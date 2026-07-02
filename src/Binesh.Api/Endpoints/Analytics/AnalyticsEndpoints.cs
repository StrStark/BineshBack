using Binesh.Application.Abstractions;
using Binesh.Application.Features.Analytics.Shared;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Analytics;

public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/data-sources", ListSources)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName(nameof(ListSources));

        routes.MapGet("/api/data-sources/{sourceId}", GetSource)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName(nameof(GetSource));

        routes.MapGet("/api/data-sources/{sourceId}/schema", GetSourceSchema)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName(nameof(GetSourceSchema));

        routes.MapPost("/api/analytics/query", Query)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName(nameof(Query));

        // Compatibility with the current Next server route names.
        routes.MapGet("/api/db-schema", GetDefaultSchema)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName(nameof(GetDefaultSchema));

        routes.MapPost("/api/db-query", Query)
            .WithTags("Analytics")
            .RequireAuthorization()
            .WithName("DbQueryCompatibility");

        return routes;
    }

    private static IResult ListSources(IBiAnalyticsService analytics) =>
        Results.Ok(ApiEnvelope.Success(analytics.ListSources()));

    private static IResult GetSource(string sourceId, IBiAnalyticsService analytics) =>
        Results.Ok(ApiEnvelope.Success(analytics.GetSource(sourceId)));

    private static IResult GetSourceSchema(string sourceId, IBiAnalyticsService analytics) =>
        Results.Ok(ApiEnvelope.Success(analytics.GetSchema(sourceId)));

    private static IResult GetDefaultSchema(IBiAnalyticsService analytics)
    {
        return Results.Ok(ApiEnvelope.Success(analytics.GetSchema(analytics.DefaultSourceId).Datasets));
    }

    private static async Task<IResult> Query(
        [FromBody] AnalyticsQueryRequest body,
        IBiAnalyticsService analytics,
        CancellationToken ct)
    {
        var request = string.IsNullOrWhiteSpace(body.SourceId)
            ? body with { SourceId = analytics.DefaultSourceId }
            : body;
        var result = await analytics.ExecuteAsync(request, ct);
        return Results.Ok(ApiEnvelope.Success(result.Rows));
    }
}
