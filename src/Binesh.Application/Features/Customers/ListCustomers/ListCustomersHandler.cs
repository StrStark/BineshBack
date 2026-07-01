using Binesh.Application.Abstractions;
using Binesh.Application.Features.Customers.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersHandler(IBineshDbContext db)
    : IRequestHandler<ListCustomersQuery, ListCustomersResponse>
{
    public async Task<ListCustomersResponse> Handle(ListCustomersQuery request, CancellationToken cancellationToken)
    {
        var query = db.Customers
            .Include(c => c.Person)
            .ThenInclude(p => p.Region)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            var sLower = s.ToLowerInvariant();
            query = query.Where(c =>
                c.Person.Name.ToLower().Contains(sLower)
                || (c.Person.Family != null && c.Person.Family.ToLower().Contains(sLower))
                || (c.Person.Mobile != null && c.Person.Mobile.Contains(s))
                || (c.Person.Phone != null && c.Person.Phone.Contains(s))
                || (c.Person.Code != null && c.Person.Code.Contains(s)));
        }

        if (request.Type is { } t)
        {
            query = query.Where(c => c.Type == t);
        }

        if (request.Active is { } a)
        {
            query = query.Where(c => c.Active == a);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = await query
            .OrderBy(c => c.Person.Family)
            .ThenBy(c => c.Person.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new ListCustomersResponse(
            page.Select(CustomerProjection.ToDto).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
