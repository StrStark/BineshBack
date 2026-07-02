using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.SalesReturns.Shared;
using Binesh.Domain.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.SalesReturns.CreateSalesReturn;

public sealed class CreateSalesReturnHandler(IBineshDbContext db, ITenantContext tenantContext)
    : IRequestHandler<CreateSalesReturnCommand, SalesReturnDto>
{
    public async Task<SalesReturnDto> Handle(CreateSalesReturnCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantContext.RequireCompanyId();
        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, cancellationToken))
        {
            throw new NotFoundException("Product", request.ProductId);
        }
        if (!await db.Customers.AnyAsync(c => c.Id == request.CounterpartyId, cancellationToken))
        {
            throw new NotFoundException("Customer", request.CounterpartyId);
        }

        var entity = SalesReturn.Create(
            companyId,
            request.Date,
            request.Price,
            request.Quantity,
            request.DeliveredQuantity,
            request.DocNumber,
            request.ProductId,
            request.CounterpartyId);

        db.SalesReturns.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return await db.SalesReturns
            .AsNoTracking()
            .Where(s => s.Id == entity.Id)
            .Select(SalesReturnProjection.ToDto)
            .SingleAsync(cancellationToken);
    }
}
