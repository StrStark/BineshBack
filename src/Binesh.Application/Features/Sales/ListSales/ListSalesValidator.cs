using FluentValidation;

namespace Binesh.Application.Features.Sales.ListSales;

public sealed class ListSalesValidator : AbstractValidator<ListSalesQuery>
{
    public ListSalesValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.Search).MaximumLength(200);

        RuleFor(q => q.To)
            .Must((q, to) => q.From is null || to is null || q.From <= to)
            .WithMessage("From must be on or before To.");
    }
}
