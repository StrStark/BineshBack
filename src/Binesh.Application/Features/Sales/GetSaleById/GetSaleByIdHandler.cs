using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Sales.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.GetSaleById;

public sealed class GetSaleByIdHandler(IBineshDbContext db)
    : IRequestHandler<GetSaleByIdQuery, SaleDto>
{
    public async Task<SaleDto> Handle(GetSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await db.Sales
            .AsNoTracking()
            .Where(s => s.Id == request.Id)
            .Select(SaleProjection.ToDto)
            .SingleOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException("Sale", request.Id);
    }
}
