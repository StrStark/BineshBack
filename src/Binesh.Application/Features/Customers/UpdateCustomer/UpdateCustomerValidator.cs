using FluentValidation;

namespace Binesh.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        When(c => c.Type is not null,
            () => RuleFor(c => c.Type!.Value).IsInEnum());

        When(c => c.PaymentReliability is not null,
            () => RuleFor(c => c.PaymentReliability!.Value).InclusiveBetween(0f, 1f));

        When(c => c.Person is not null, () =>
        {
            RuleFor(c => c.Person!.Name).MaximumLength(150);
            RuleFor(c => c.Person!.Family).MaximumLength(150);
            RuleFor(c => c.Person!.Code).MaximumLength(50);
            RuleFor(c => c.Person!.Phone).MaximumLength(50);
            RuleFor(c => c.Person!.Mobile).MaximumLength(30);
            RuleFor(c => c.Person!.Fax).MaximumLength(50);
            RuleFor(c => c.Person!.Pelak).MaximumLength(50);
            RuleFor(c => c.Person!.Address).MaximumLength(500);
        });
    }
}
