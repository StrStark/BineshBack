using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Products.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.GetProductByCode;

public sealed record GetProductByCodeQuery(string ProductCode) : IRequest<ProductDto>;

public sealed class GetProductByCodeHandler(IBineshDbContext db)
    : IRequestHandler<GetProductByCodeQuery, ProductDto>
{
    public async Task<ProductDto> Handle(GetProductByCodeQuery request, CancellationToken cancellationToken)
    {
        var p = await db.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProductCode == request.ProductCode, cancellationToken)
            ?? throw new NotFoundException("Product", request.ProductCode);

        return ProductProjection.ToDto(p);
    }
}
