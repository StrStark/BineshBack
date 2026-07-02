using Binesh.Application.Abstractions;
using Binesh.Application.Features.Financial.Shared;
using Binesh.Domain.Financial;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.CreateFinancialEntry;

public sealed class CreateFinancialEntryHandler(IBineshDbContext db, ITenantContext tenantContext)
    : IRequestHandler<CreateFinancialEntryCommand, FinancialEntryDto>
{
    public async Task<FinancialEntryDto> Handle(CreateFinancialEntryCommand request, CancellationToken cancellationToken)
    {
        var entry = FinancialEntry.Create(
            tenantContext.RequireCompanyId(),
            request.Code,
            request.Name,
            request.Type,
            request.Debit,
            request.Credit);
        db.FinancialEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return await db.FinancialEntries
            .AsNoTracking()
            .Where(e => e.Id == entry.Id)
            .Select(FinancialEntryProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
