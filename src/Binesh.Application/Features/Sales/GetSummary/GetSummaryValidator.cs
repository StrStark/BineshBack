using FluentValidation;

namespace Binesh.Application.Features.Sales.GetSummary;

public sealed class GetSummaryValidator : AbstractValidator<GetSummaryQuery>
{
    private const int MaxRangeDays = 366;

    public GetSummaryValidator()
    {
        RuleFor(q => q.From)
            .NotEqual(default(DateOnly))
            .WithMessage("From date is required.");

        RuleFor(q => q.To)
            .NotEqual(default(DateOnly))
            .WithMessage("To date is required.");

        // Cross-field rules attached to a specific property so the error key
        // in the ProblemDetails response is meaningful (not "").
        RuleFor(q => q.From)
            .Must((q, from) => from <= q.To)
            .WithMessage("From must be on or before To.");

        RuleFor(q => q.To)
            .Must((q, to) => to.DayNumber - q.From.DayNumber <= MaxRangeDays)
            .WithMessage($"Date range cannot exceed {MaxRangeDays} days.");
    }
}
