using FluentValidation;

namespace Binesh.Identity.Features.Users.SetProfileImage;

public sealed class SetProfileImageValidator : AbstractValidator<SetProfileImageCommand>
{
    public SetProfileImageValidator()
    {
        RuleFor(c => c.UserId).NotEqual(Guid.Empty);
        RuleFor(c => c.ObjectKey).MaximumLength(255);
        RuleFor(c => c.ObjectKey)
            .Must(k => k!.StartsWith("profile-images/", StringComparison.Ordinal))
            .When(c => !string.IsNullOrEmpty(c.ObjectKey))
            .WithMessage("ObjectKey must live under the profile-images/ prefix.");
    }
}
