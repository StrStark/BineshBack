using FluentValidation;

namespace Binesh.Application.Features.Financial.CreateFinancialEntry;

public sealed class CreateFinancialEntryValidator : AbstractValidator<CreateFinancialEntryCommand>
{
    public CreateFinancialEntryValidator()
    {
        RuleFor(c => c.Code).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Name).NotEmpty().MaximumLength(256);
        RuleFor(c => c.Type).NotEmpty().MaximumLength(128);
        RuleFor(c => c.Debit).GreaterThanOrEqualTo(0);
        RuleFor(c => c.Credit).GreaterThanOrEqualTo(0);
    }
}
