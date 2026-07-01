using FluentValidation;

namespace Binesh.Identity.Features.Auth.SignOut;

public sealed class SignOutValidator : AbstractValidator<SignOutCommand>
{
    public SignOutValidator()
    {
        RuleFor(c => c.RefreshToken).NotEmpty();
    }
}
