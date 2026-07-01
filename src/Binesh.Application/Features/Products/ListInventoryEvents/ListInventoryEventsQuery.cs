using Binesh.Application.Abstractions;
using Binesh.Application.Features.Products.Shared;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.ListInventoryEvents;

public sealed record ListInventoryEventsQuery(
    Guid ProductId,
    int Page,
    int PageSize)
    : IRequest<ListInventoryEventsResponse>;

public sealed record ListInventoryEventsResponse(
    IReadOnlyList<InventoryEventDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class ListInventoryEventsValidator : AbstractValidator<ListInventoryEventsQuery>
{
    public ListInventoryEventsValidator()
    {
        RuleFor(q => q.ProductId).NotEmpty();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
    }
}

public sealed class ListInventoryEventsHandler(IBineshDbContext db)
    : IRequestHandler<ListInventoryEventsQuery, ListInventoryEventsResponse>
{
    public async Task<ListInventoryEventsResponse> Handle(ListInventoryEventsQuery request, CancellationToken cancellationToken)
    {
        var query = db.InventoryEvents
            .AsNoTracking()
            .Where(e => e.ProductId == request.ProductId);

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(e => e.Date)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new ListInventoryEventsResponse(
            rows.Select(ProductProjection.ToDto).ToList(),
            totalCount,
            request.Page,
            request.PageSize);
    }
}
