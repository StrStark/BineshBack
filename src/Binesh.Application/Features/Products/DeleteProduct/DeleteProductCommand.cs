using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest<Unit>;

public sealed class DeleteProductHandler(IBineshDbContext db)
    : IRequestHandler<DeleteProductCommand, Unit>
{
    public async Task<Unit> Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        // CASCADE on Product → InventoryEvents handles event rows.
        var affected = await db.Products
            .Where(p => p.Id == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException("Product", request.Id);
        }

        return Unit.Value;
    }
}
