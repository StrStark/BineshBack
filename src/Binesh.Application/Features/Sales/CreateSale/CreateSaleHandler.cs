using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Sales.Shared;
using Binesh.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.CreateSale;

public sealed class CreateSaleHandler(IBineshDbContext db)
    : IRequestHandler<CreateSaleCommand, SaleDto>
{
    public async Task<SaleDto> Handle(CreateSaleCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
        {
            throw new NotFoundException("Product", request.ProductId);
        }
        if (!await db.Customers.AnyAsync(c => c.Id == request.CounterpartyId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CounterpartyId);
        }

        var sale = Sale.Create(
            request.Date,
            request.Price,
            request.Quantity,
            request.DeliveredQuantity,
            request.DocNumber,
            request.ProductId,
            request.CounterpartyId);

        db.Sales.Add(sale);
        await db.SaveChangesAsync(cancellationToken);

        return await db.Sales
            .AsNoTracking()
            .Where(s => s.Id == sale.Id)
            .Select(SaleProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
