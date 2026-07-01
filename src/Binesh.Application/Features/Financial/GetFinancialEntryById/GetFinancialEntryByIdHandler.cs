using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Financial.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Financial.GetFinancialEntryById;

public sealed class GetFinancialEntryByIdHandler(IBineshDbContext db)
    : IRequestHandler<GetFinancialEntryByIdQuery, FinancialEntryDto>
{
    public async Task<FinancialEntryDto> Handle(GetFinancialEntryByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await db.FinancialEntries
            .AsNoTracking()
            .Where(e => e.Id == request.Id)
            .Select(FinancialEntryProjection.ToDto)
            .SingleOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException("FinancialEntry", request.Id);
    }
}
