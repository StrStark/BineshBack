using Binesh.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.GetSummary;

public sealed class GetSummaryHandler(IBineshDbContext db)
    : IRequestHandler<GetSummaryQuery, GetSummaryResponse>
{
    public async Task<GetSummaryResponse> Handle(
        GetSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var fromUtc = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // GROUP BY translated to SQL — the database does the aggregation, not us.
        // This is the pattern every aggregating slice must follow; the old code
        // materialized full tables in memory then grouped, which falls over
        // past ~100k rows.
        var rows = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= fromUtc && s.Date < toUtcExclusive)
            .GroupBy(s => s.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Revenue = g.Sum(x => x.Price),
                Count = g.Count(),
            })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken);

        var totalRevenue = rows.Sum(r => r.Revenue);
        var orderCount = rows.Sum(r => r.Count);
        var average = orderCount > 0 ? (decimal)totalRevenue / orderCount : 0m;

        var byDay = rows
            .Select(r => new DailyBreakdown(DateOnly.FromDateTime(r.Date), r.Revenue, r.Count))
            .ToList();

        return new GetSummaryResponse(totalRevenue, orderCount, average, byDay);
    }
}
