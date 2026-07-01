using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Sales.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.UpdateSale;

public sealed class UpdateSaleHandler(IBineshDbContext db)
    : IRequestHandler<UpdateSaleCommand, SaleDto>
{
    public async Task<SaleDto> Handle(UpdateSaleCommand request, CancellationToken cancellationToken)
    {
        var sale = await db.Sales.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Sale", request.Id);

        if (request.ProductId is { } pid
            && pid != sale.ProductId
            && !await db.Products.AnyAsync(p => p.Id == pid, cancellationToken))
        {
            throw new NotFoundException("Product", pid);
        }

        if (request.CounterpartyId is { } cid
            && cid != sale.CounterpartyId
            && !await db.Customers.AnyAsync(c => c.Id == cid, cancellationToken))
        {
            throw new NotFoundException("Customer", cid);
        }

        sale.Update(
            request.Date,
            request.Price,
            request.Quantity,
            request.DeliveredQuantity,
            request.DocNumber,
            request.ProductId,
            request.CounterpartyId);

        await db.SaveChangesAsync(cancellationToken);

        return await db.Sales
            .AsNoTracking()
            .Where(s => s.Id == sale.Id)
            .Select(SaleProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
