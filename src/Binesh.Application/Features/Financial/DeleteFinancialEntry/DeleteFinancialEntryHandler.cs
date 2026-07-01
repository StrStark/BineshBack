using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.DeleteFinancialEntry;

public sealed class DeleteFinancialEntryHandler(IBineshDbContext db) : IRequestHandler<DeleteFinancialEntryCommand>
{
    public async Task Handle(DeleteFinancialEntryCommand request, CancellationToken cancellationToken)
    {
        var affected = await db.FinancialEntries
            .Where(e => e.Id == request.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (affected == 0)
        {
            throw new NotFoundException("FinancialEntry", request.Id);
        }
    }
}
