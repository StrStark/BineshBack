using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Products.Shared;
using Binesh.Domain.Products;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.AddInventoryEvent;

public sealed record AddInventoryEventCommand(
    Guid ProductId,
    InventoryEventType Type,
    DateTime Date,
    float Quantity,
    long UnitPrice,
    long TotalPrice,
    int FactorNumber,
    string? Value1,
    string? Value2,
    string? Value3,
    string? Description)
    : IRequest<InventoryEventDto>;

public sealed class AddInventoryEventValidator : AbstractValidator<AddInventoryEventCommand>
{
    public AddInventoryEventValidator()
    {
        RuleFor(c => c.ProductId).NotEmpty();
        RuleFor(c => c.Type).IsInEnum().NotEqual(InventoryEventType.None);
        RuleFor(c => c.Quantity).GreaterThanOrEqualTo(0);
        RuleFor(c => c.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(c => c.TotalPrice).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Description).MaximumLength(500);
    }
}

public sealed class AddInventoryEventHandler(IBineshDbContext db)
    : IRequestHandler<AddInventoryEventCommand, InventoryEventDto>
{
    public async Task<InventoryEventDto> Handle(AddInventoryEventCommand request, CancellationToken cancellationToken)
    {
        var productExists = await db.Products
            .AnyAsync(p => p.Id == request.ProductId, cancellationToken);
        if (!productExists)
        {
            throw new NotFoundException("Product", request.ProductId);
        }

        var ev = InventoryEvent.Create(
            request.ProductId,
            request.Type,
            request.Date,
            request.Quantity,
            request.UnitPrice,
            request.TotalPrice,
            request.FactorNumber,
            request.Value1,
            request.Value2,
            request.Value3,
            request.Description);

        db.InventoryEvents.Add(ev);
        await db.SaveChangesAsync(cancellationToken);

        return ProductProjection.ToDto(ev);
    }
}
