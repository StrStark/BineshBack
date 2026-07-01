using MediatR;

namespace Binesh.Application.Features.SalesReturns.GetReturnsSummary;

/// <summary>
/// Per-day returns aggregation over a date range — parallel to
/// <c>/api/sales/summary</c>.
/// </summary>
public sealed record GetReturnsSummaryQuery(DateOnly From, DateOnly To)
    : IRequest<GetReturnsSummaryResponse>;

public sealed record GetReturnsSummaryResponse(
    long TotalReturned,
    int ReturnCount,
    decimal AverageReturnValue,
    IReadOnlyList<DailyReturnsBreakdown> ByDay);

public sealed record DailyReturnsBreakdown(DateOnly Date, long Returned, int ReturnCount);
