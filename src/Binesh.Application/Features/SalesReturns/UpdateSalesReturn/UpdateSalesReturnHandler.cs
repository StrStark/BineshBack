using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.UpdateSalesReturn;

public sealed class UpdateSalesReturnHandler(IBineshDbContext db)
    : IRequestHandler<UpdateSalesReturnCommand, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(UpdateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var entity = await db.SalesReturns.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("SalesReturn", request.Id);

        if (request.ProductId is { } pid
            && pid != entity.ProductId
            && !await db.Products.AnyAsync(p => p.Id == pid, cancellationToken))
        {
            throw new NotFoundException("Product", pid);
        }

        if (request.CounterpartyId is { } cid
            && cid != entity.CounterpartyId
            && !await db.Customers.AnyAsync(c => c.Id == cid, cancellationToken))
        {
            throw new NotFoundException("Customer", cid);
        }

        entity.Update(
            request.Date,
            request.Price,
            request.Quantity,
            request.DeliveredQuantity,
            request.DocNumber,
            request.ProductId,
            request.CounterpartyId);

        await db.SaveChangesAsync(cancellationToken);

        return await db.SalesReturns
            .AsNoTracking()
            .Where(s => s.Id == entity.Id)
            .Select(SalesReturnProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
