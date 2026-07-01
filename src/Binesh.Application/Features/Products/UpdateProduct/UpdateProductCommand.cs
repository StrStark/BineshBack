using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Products.Shared;
using Binesh.Domain.Products;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    ProductType? Type,
    string? ProductCode,
    string? ProductDescription,
    string? DetailedType)
    : IRequest<ProductDto>;

public sealed class UpdateProductValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        When(c => c.Type is not null,
            () => RuleFor(c => c.Type!.Value).IsInEnum());
        RuleFor(c => c.ProductCode).MaximumLength(100);
        RuleFor(c => c.ProductDescription).MaximumLength(500);
        RuleFor(c => c.DetailedType).MaximumLength(200);
    }
}

public sealed class UpdateProductHandler(IBineshDbContext db)
    : IRequestHandler<UpdateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await db.Products
            .SingleOrDefaultAsync(p => p.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Product", request.Id);

        if (request.ProductCode is not null
            && request.ProductCode != product.ProductCode)
        {
            var duplicate = await db.Products.AnyAsync(
                p => p.ProductCode == request.ProductCode && p.Id != request.Id,
                cancellationToken);
            if (duplicate)
            {
                throw new ConflictException(
                    $"A product with code '{request.ProductCode}' already exists.",
                    "product.duplicate_code");
            }
        }

        product.Update(request.Type, request.ProductCode, request.ProductDescription, request.DetailedType);
        await db.SaveChangesAsync(cancellationToken);

        return ProductProjection.ToDto(product);
    }
}
