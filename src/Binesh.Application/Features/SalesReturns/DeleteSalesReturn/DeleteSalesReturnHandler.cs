using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.DeleteSalesReturn;

public sealed class DeleteSalesReturnHandler(IBineshDbContext db) : IRequestHandler<DeleteSalesReturnCommand>
{
    public async Task Handle(DeleteSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var affected = await db.SalesReturns
            .Where(s => s.Id == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException("SalesReturn", request.Id);
        }
    }
}
