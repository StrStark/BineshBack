using FluentValidation;

namespace Binesh.Application.Features.Financial.ListFinancialEntries;

public sealed class ListFinancialEntriesValidator : AbstractValidator<ListFinancialEntriesQuery>
{
    public ListFinancialEntriesValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.Search).MaximumLength(200);
        RuleFor(q => q.Type).MaximumLength(128);
    }
}
