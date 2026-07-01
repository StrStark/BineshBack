using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.GetSalesReturnById;

public sealed class GetSalesReturnByIdHandler(IBineshDbContext db)
    : IRequestHandler<GetSalesReturnByIdQuery, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(GetSalesReturnByIdQuery request, CancellationToken cancellationToken)
    {
        var dto = await db.SalesReturns
            .AsNoTracking()
            .Where(s => s.Id == request.Id)
            .Select(SalesReturnProjection.ToDto)
            .SingleOrDefaultAsync(cancellationToken);

        return dto ?? throw new NotFoundException("SalesReturn", request.Id);
    }
}
