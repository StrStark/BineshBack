using FluentValidation;

namespace Binesh.Application.Features.SalesReturns.GetReturnsSummary;

public sealed class GetReturnsSummaryValidator : AbstractValidator<GetReturnsSummaryQuery>
{
    private const int MaxRangeDays = 366;

    public GetReturnsSummaryValidator()
    {
        RuleFor(q => q.From).NotEqual(default(DateOnly)).WithMessage("From date is required.");
        RuleFor(q => q.To).NotEqual(default(DateOnly)).WithMessage("To date is required.");
        RuleFor(q => q.To)
            .Must((q, to) => to >= q.From)
            .WithMessage("From must be on or before To.");
        RuleFor(q => q.To)
            .Must((q, to) => to.DayNumber - q.From.DayNumber <= MaxRangeDays)
            .WithMessage($"Date range cannot exceed {MaxRangeDays} days.");
    }
}
