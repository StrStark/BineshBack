using FluentValidation;

namespace Binesh.Application.Features.Sales.UpdateSale;

public sealed class UpdateSaleValidator : AbstractValidator<UpdateSaleCommand>
{
    public UpdateSaleValidator()
    {
        RuleFor(c => c.Price!.Value).GreaterThanOrEqualTo(0).When(c => c.Price.HasValue);
        RuleFor(c => c.Quantity!.Value).GreaterThan(0f).When(c => c.Quantity.HasValue);
        RuleFor(c => c.DeliveredQuantity!.Value).GreaterThanOrEqualTo(0f).When(c => c.DeliveredQuantity.HasValue);
        RuleFor(c => c.ProductId!.Value).NotEqual(Guid.Empty).When(c => c.ProductId.HasValue);
        RuleFor(c => c.CounterpartyId!.Value).NotEqual(Guid.Empty).When(c => c.CounterpartyId.HasValue);
    }
}
