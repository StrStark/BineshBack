using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.DeleteSale;

public sealed class DeleteSaleHandler(IBineshDbContext db) : IRequestHandler<DeleteSaleCommand>
{
    public async Task Handle(DeleteSaleCommand request, CancellationToken cancellationToken)
    {
        var affected = await db.Sales
            .Where(s => s.Id == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException("Sale", request.Id);
        }
    }
}
