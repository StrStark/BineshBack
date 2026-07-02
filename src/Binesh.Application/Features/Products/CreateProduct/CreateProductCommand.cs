using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Products.Shared;
using Binesh.Domain.Products;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.CreateProduct;

public sealed record CreateProductCommand(
    ProductType Type,
    string ProductCode,
    string ProductDescription,
    string? DetailedType)
    : IRequest<ProductDto>;

public sealed class CreateProductValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductValidator()
    {
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.ProductCode).NotEmpty().MaximumLength(100);
        RuleFor(c => c.ProductDescription).NotEmpty().MaximumLength(500);
        RuleFor(c => c.DetailedType).MaximumLength(200);
    }
}

public sealed class CreateProductHandler(IBineshDbContext db, ITenantContext tenantContext)
    : IRequestHandler<CreateProductCommand, ProductDto>
{
    public async Task<ProductDto> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantContext.RequireCompanyId();
        var exists = await db.Products
            .AnyAsync(p => p.ProductCode == request.ProductCode, cancellationToken);
        if (exists)
        {
            throw new ConflictException(
                $"A product with code '{request.ProductCode}' already exists.",
                "product.duplicate_code");
        }

        var product = Product.Create(
            companyId,
            request.Type,
            request.ProductCode,
            request.ProductDescription,
            request.DetailedType ?? string.Empty);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ProductProjection.ToDto(product);
    }
}
