using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Financial.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.UpdateFinancialEntry;

public sealed class UpdateFinancialEntryHandler(IBineshDbContext db)
    : IRequestHandler<UpdateFinancialEntryCommand, FinancialEntryDto>
{
    public async Task<FinancialEntryDto> Handle(UpdateFinancialEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = await db.FinancialEntries.SingleOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("FinancialEntry", request.Id);

        entry.Update(request.Code, request.Name, request.Type, request.Debit, request.Credit);
        await db.SaveChangesAsync(cancellationToken);

        return await db.FinancialEntries
            .AsNoTracking()
            .Where(e => e.Id == entry.Id)
            .Select(FinancialEntryProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
