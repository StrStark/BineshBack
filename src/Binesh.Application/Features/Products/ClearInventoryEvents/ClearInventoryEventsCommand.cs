using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.ClearInventoryEvents;

/// <summary>
/// Deletes every inventory event for a product (does NOT delete the product itself).
/// Useful for ETL re-import scenarios.
/// </summary>
public sealed record ClearInventoryEventsCommand(Guid ProductId) : IRequest<int>;

public sealed class ClearInventoryEventsHandler(IBineshDbContext db)
    : IRequestHandler<ClearInventoryEventsCommand, int>
{
    public async Task<int> Handle(ClearInventoryEventsCommand request, CancellationToken cancellationToken)
    {
        var productExists = await db.Products
            .AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            throw new NotFoundException("Product", request.ProductId);
        }

        return await db.InventoryEvents
            .Where(e => e.ProductId == request.ProductId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
