using Binesh.Application.Features.Analytics.Shared;

namespace Binesh.Application.Abstractions;

public interface IBiAnalyticsService
{
    string DefaultSourceId { get; }
    IReadOnlyList<BiDataSourceDto> ListSources();
    BiDataSourceDetailDto GetSource(string sourceId);
    BiSourceSchemaDto GetSchema(string sourceId);
    Task<AnalyticsQueryResult> ExecuteAsync(AnalyticsQueryRequest request, CancellationToken cancellationToken);
}
