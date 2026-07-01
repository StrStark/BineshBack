using FluentValidation;

namespace Binesh.Application.Features.Ai.AskAi;

public sealed class AskAiValidator : AbstractValidator<AskAiCommand>
{
    public AskAiValidator()
    {
        RuleFor(c => c.Message).NotEmpty().MaximumLength(8000);
    }
}
