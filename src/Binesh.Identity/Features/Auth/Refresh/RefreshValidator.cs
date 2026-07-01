using FluentValidation;

namespace Binesh.Identity.Features.Auth.Refresh;

public sealed class RefreshValidator : AbstractValidator<RefreshCommand>
{
    public RefreshValidator()
    {
        RuleFor(c => c.RefreshToken)
            .NotEmpty()
            .MinimumLength(40);
    }
}
