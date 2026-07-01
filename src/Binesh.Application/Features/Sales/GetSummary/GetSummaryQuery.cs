using MediatR;

namespace Binesh.Application.Features.Sales.GetSummary;

/// <summary>
/// Returns a per-day revenue + order-count summary over a date range,
/// plus totals and average order value.
///
/// Round 4 reference slice — every other feature follows this exact shape:
///   - Query / Command : IRequest&lt;Response&gt;      (this file)
///   - Response record                            (this file)
///   - Validator        : AbstractValidator&lt;Query&gt; (sibling file)
///   - Handler          : IRequestHandler&lt;Query, Response&gt; (sibling file)
///   - Endpoint mapping                           (in Binesh.Api/Endpoints/)
///   - Integration test                           (in Binesh.Api.IntegrationTests/Features/)
/// </summary>
public sealed record GetSummaryQuery(DateOnly From, DateOnly To)
    : IRequest<GetSummaryResponse>;

public sealed record GetSummaryResponse(
    long TotalRevenue,
    int OrderCount,
    decimal AverageOrderValue,
    IReadOnlyList<DailyBreakdown> ByDay);

public sealed record DailyBreakdown(DateOnly Date, long Revenue, int OrderCount);
