using Binesh.Application.Abstractions;
using Binesh.Application.Features.Financial.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.ListFinancialEntries;

public sealed class ListFinancialEntriesHandler(IBineshDbContext db)
    : IRequestHandler<ListFinancialEntriesQuery, ListFinancialEntriesResponse>
{
    public async Task<ListFinancialEntriesResponse> Handle(ListFinancialEntriesQuery request, CancellationToken cancellationToken)
    {
        var query = db.FinancialEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Type))
        {
            var type = request.Type.Trim();
            query = query.Where(e => e.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.Code.ToLower().Contains(s) || e.Name.ToLower().Contains(s));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(FinancialEntryProjection.ToDto)
            .ToListAsync(cancellationToken);

        return new ListFinancialEntriesResponse(items, totalCount, request.Page, request.PageSize);
    }
}
