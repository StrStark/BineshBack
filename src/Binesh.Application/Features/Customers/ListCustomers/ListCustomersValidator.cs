using FluentValidation;

namespace Binesh.Application.Features.Customers.ListCustomers;

public sealed class ListCustomersValidator : AbstractValidator<ListCustomersQuery>
{
    public ListCustomersValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.Search).MaximumLength(200);
    }
}
