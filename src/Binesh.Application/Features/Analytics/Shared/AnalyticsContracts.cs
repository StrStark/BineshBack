using System.Text.Json;

namespace Binesh.Application.Features.Analytics.Shared;

public sealed record BiDataSourceDto(
    string Id,
    string Label,
    string Description,
    string Provider,
    bool Enabled,
    int DimensionCount,
    int MeasureCount)
{
    public string Name => Label;
}

public sealed record BiDataSourceDetailDto(
    string Id,
    string Label,
    string Description,
    string Provider,
    bool Enabled,
    IReadOnlyList<BiFieldDto> Fields,
    IReadOnlyList<ChartTypeDto> ChartTypes,
    IReadOnlyList<string> Aggregations)
{
    public string Name => Label;
}

public sealed record BiSourceSchemaDto(
    string SourceId,
    IReadOnlyList<BiDatasetDto> Datasets,
    IReadOnlyList<ChartTypeDto> ChartTypes,
    IReadOnlyList<string> Aggregations);

public sealed record BiDatasetDto(
    string Id,
    string Label,
    IReadOnlyList<BiFieldDto> Fields);

public sealed record BiFieldDto(
    string Name,
    string Label,
    string Type,
    string Role,
    IReadOnlyList<string> AllowedAggregations,
    IReadOnlyList<string> FilterOperators);

public sealed record ChartTypeDto(string Id, string Label, string Icon);

public sealed record AnalyticsQueryRequest(
    string? SourceId,
    string? DatasetId,
    string? LabelField,
    string? ValueField,
    string? Aggregation,
    IReadOnlyList<string>? GroupBy,
    IReadOnlyList<AnalyticsValueDto>? Values,
    IReadOnlyList<AnalyticsFilterDto>? Filters,
    AnalyticsOrderByDto? OrderBy,
    int? Limit,
    bool Raw = false,
    string? Table = null);

public sealed record AnalyticsValueDto(
    string Field,
    string Aggregation,
    string? Alias);

public sealed record AnalyticsFilterDto(
    string Field,
    string Operator,
    JsonElement? Value);

public sealed record AnalyticsOrderByDto(
    string Field,
    string Direction);

public sealed record AnalyticsQueryResult(
    string SourceId,
    string DatasetId,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
