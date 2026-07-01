using FluentValidation;

namespace Binesh.Application.Features.Sales.CreateSale;

public sealed class CreateSaleValidator : AbstractValidator<CreateSaleCommand>
{
    public CreateSaleValidator()
    {
        RuleFor(c => c.Date).NotEqual(default(DateTime)).WithMessage("Date is required.");
        RuleFor(c => c.Price).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Quantity).GreaterThan(0f);
        RuleFor(c => c.DeliveredQuantity).GreaterThanOrEqualTo(0f);
        RuleFor(c => c.ProductId).NotEqual(Guid.Empty).WithMessage("ProductId is required.");
        RuleFor(c => c.CounterpartyId).NotEqual(Guid.Empty).WithMessage("CounterpartyId is required.");
    }
}
