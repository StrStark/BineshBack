using Binesh.Application.Abstractions;
using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.ListSalesReturns;

public sealed class ListSalesReturnsHandler(IBineshDbContext db)
    : IRequestHandler<ListSalesReturnsQuery, ListSalesReturnsResponse>
{
    public async Task<ListSalesReturnsResponse> Handle(ListSalesReturnsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesReturns.AsNoTracking().AsQueryable();

        if (request.From is { } from)
        {
            var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.Date >= fromUtc);
        }

        if (request.To is { } to)
        {
            var toUtcExclusive = to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(s => s.Date < toUtcExclusive);
        }

        if (request.CustomerId is { } cid)
        {
            query = query.Where(s => s.CounterpartyId == cid);
        }

        if (request.ProductId is { } pid)
        {
            query = query.Where(s => s.ProductId == pid);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            var sLower = s.ToLowerInvariant();
            query = query.Where(sr =>
                sr.Product.ProductCode.ToLower().Contains(sLower)
                || sr.Product.ProductDescription.ToLower().Contains(sLower)
                || sr.Counterparty.Person.Name.ToLower().Contains(sLower)
                || (sr.Counterparty.Person.Family != null
                    && sr.Counterparty.Person.Family.ToLower().Contains(sLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.Date)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(SalesReturnProjection.ToDto)
            .ToListAsync(cancellationToken);

        return new ListSalesReturnsResponse(items, totalCount, request.Page, request.PageSize);
    }
}
