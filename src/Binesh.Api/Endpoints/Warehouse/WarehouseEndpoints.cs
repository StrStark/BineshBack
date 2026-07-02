using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Analytics.Shared;
using System.Text.Json;

namespace Binesh.Api.Endpoints.Warehouse;

public static class WarehouseEndpoints
{
    private static readonly JsonElement EmptyValue = JsonDocument.Parse("\"\"").RootElement.Clone();

    public static IEndpointRouteBuilder MapWarehouseEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/warehouse")
            .WithTags("Warehouse")
            .RequireAuthorization();

        group.MapGet("/", ListItems).WithName(nameof(ListItems));
        group.MapPost("/", ReadOnlyMutation).WithName(nameof(ReadOnlyMutation));
        group.MapGet("/status", Status).WithName(nameof(Status));
        group.MapGet("/flow", Flow).WithName(nameof(Flow));
        group.MapGet("/bubble", Bubble).WithName(nameof(Bubble));
        group.MapGet("/fast-moving", FastMoving).WithName(nameof(FastMoving));

        return routes;
    }

    private static Task<IResult> ListItems(IBiAnalyticsService analytics, CancellationToken ct) =>
        QueryRows(analytics, new AnalyticsQueryRequest(
            Source(analytics), "WarehouseItem", null, null, null, null, null, null, null, 500, Raw: true), ct);

    private static IResult ReadOnlyMutation() =>
        Results.Conflict(ApiEnvelope.Error("Warehouse source is read-only; update the SQL Server source system.", StatusCodes.Status409Conflict));

    private static async Task<IResult> Status(IBiAnalyticsService analytics, CancellationToken ct)
    {
        var result = await analytics.ExecuteAsync(new AnalyticsQueryRequest(
            Source(analytics),
            "WarehouseItem",
            LabelField: "warehouse",
            ValueField: null,
            Aggregation: null,
            GroupBy: ["warehouse"],
            Values:
            [
                new AnalyticsValueDto("quantity", "sum", "used"),
                new AnalyticsValueDto("maxStock", "sum", "capacity"),
                new AnalyticsValueDto("productName", "count", "items"),
            ],
            Filters: null,
            OrderBy: new AnalyticsOrderByDto("warehouse", "asc"),
            Limit: 200), ct);

        return Results.Ok(ApiEnvelope.Success(new { warehouses = result.Rows }));
    }

    private static Task<IResult> Flow(IBiAnalyticsService analytics, CancellationToken ct) =>
        QueryRows(analytics, new AnalyticsQueryRequest(
            Source(analytics),
            "WarehouseTransaction",
            null,
            null,
            null,
            null,
            null,
            null,
            new AnalyticsOrderByDto("date", "asc"),
            365,
            Raw: true), ct);

    private static Task<IResult> Bubble(IBiAnalyticsService analytics, CancellationToken ct) =>
        QueryRows(analytics, new AnalyticsQueryRequest(
            Source(analytics),
            "WarehouseItem",
            null,
            null,
            null,
            null,
            null,
            null,
            new AnalyticsOrderByDto("quantity", "desc"),
            100,
            Raw: true), ct);

    private static Task<IResult> FastMoving(IBiAnalyticsService analytics, CancellationToken ct) =>
        QueryRows(analytics, new AnalyticsQueryRequest(
            Source(analytics),
            "Sale",
            LabelField: "KDesc",
            ValueField: "UnitValue1",
            Aggregation: "sum",
            GroupBy: ["KDesc"],
            Values: [new AnalyticsValueDto("UnitValue1", "sum", "quantity")],
            Filters: [new AnalyticsFilterDto("KDesc", "ne", EmptyValue)],
            OrderBy: new AnalyticsOrderByDto("quantity", "desc"),
            Limit: 20), ct);

    private static async Task<IResult> QueryRows(
        IBiAnalyticsService analytics,
        AnalyticsQueryRequest request,
        CancellationToken ct)
    {
        var result = await analytics.ExecuteAsync(request, ct);
        return Results.Ok(ApiEnvelope.Success(result.Rows));
    }

    private static string Source(IBiAnalyticsService analytics) =>
        analytics.DefaultSourceId;
}
