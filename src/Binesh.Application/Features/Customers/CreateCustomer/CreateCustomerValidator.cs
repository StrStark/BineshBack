using FluentValidation;

namespace Binesh.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(c => c.Type).IsInEnum();
        RuleFor(c => c.PaymentReliability).InclusiveBetween(0f, 1f);

        RuleFor(c => c.Person.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(c => c.Person.Family).MaximumLength(150);
        RuleFor(c => c.Person.Code).MaximumLength(50);
        RuleFor(c => c.Person.Phone).MaximumLength(50);
        RuleFor(c => c.Person.Mobile).MaximumLength(30);
        RuleFor(c => c.Person.Fax).MaximumLength(50);
        RuleFor(c => c.Person.Pelak).MaximumLength(50);
        RuleFor(c => c.Person.Address).MaximumLength(500);
    }
}
