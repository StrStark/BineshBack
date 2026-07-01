using Binesh.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.GetReturnsSummary;

public sealed class GetReturnsSummaryHandler(IBineshDbContext db)
    : IRequestHandler<GetReturnsSummaryQuery, GetReturnsSummaryResponse>
{
    public async Task<GetReturnsSummaryResponse> Handle(
        GetReturnsSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromUtc = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtcExclusive = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await db.SalesReturns
            .AsNoTracking()
            .Where(s => s.Date >= fromUtc && s.Date < toUtcExclusive)
            .GroupBy(s => s.Date.Date)
            .Select(g => new
            {
                Date = g.Key,
                Returned = g.Sum(x => x.Price),
                Count = g.Count(),
            })
            .OrderBy(r => r.Date)
            .ToListAsync(cancellationToken);

        var byDay = rows
            .Select(r => new DailyReturnsBreakdown(DateOnly.FromDateTime(r.Date), r.Returned, r.Count))
            .ToList();

        var totalReturned = byDay.Sum(d => d.Returned);
        var returnCount = byDay.Sum(d => d.ReturnCount);
        var avg = returnCount == 0 ? 0m : (decimal)totalReturned / returnCount;

        return new GetReturnsSummaryResponse(totalReturned, returnCount, avg, byDay);
    }
}
