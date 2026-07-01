using Binesh.Application.Abstractions;
using Binesh.Application.Features.Sales.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.ListSales;

public sealed class ListSalesHandler(IBineshDbContext db)
    : IRequestHandler<ListSalesQuery, ListSalesResponse>
{
    public async Task<ListSalesResponse> Handle(ListSalesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Sales.AsNoTracking().AsQueryable();

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
            query = query.Where(sale =>
                sale.Product.ProductCode.ToLower().Contains(sLower)
                || sale.Product.ProductDescription.ToLower().Contains(sLower)
                || sale.Counterparty.Person.Name.ToLower().Contains(sLower)
                || (sale.Counterparty.Person.Family != null
                    && sale.Counterparty.Person.Family.ToLower().Contains(sLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.Date)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(SaleProjection.ToDto)
            .ToListAsync(cancellationToken);

        return new ListSalesResponse(items, totalCount, request.Page, request.PageSize);
    }
}
