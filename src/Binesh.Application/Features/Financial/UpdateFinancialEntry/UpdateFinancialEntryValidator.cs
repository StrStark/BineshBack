using FluentValidation;

namespace Binesh.Application.Features.Financial.UpdateFinancialEntry;

public sealed class UpdateFinancialEntryValidator : AbstractValidator<UpdateFinancialEntryCommand>
{
    public UpdateFinancialEntryValidator()
    {
        RuleFor(c => c.Code!).NotEmpty().MaximumLength(64).When(c => c.Code is not null);
        RuleFor(c => c.Name!).NotEmpty().MaximumLength(256).When(c => c.Name is not null);
        RuleFor(c => c.Type!).NotEmpty().MaximumLength(128).When(c => c.Type is not null);
        RuleFor(c => c.Debit!.Value).GreaterThanOrEqualTo(0).When(c => c.Debit.HasValue);
        RuleFor(c => c.Credit!.Value).GreaterThanOrEqualTo(0).When(c => c.Credit.HasValue);
    }
}
