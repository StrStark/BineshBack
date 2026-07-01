using FluentValidation;

namespace Binesh.Identity.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileValidator()
    {
        RuleFor(c => c.FirstName).MaximumLength(100);
        RuleFor(c => c.LastName).MaximumLength(100);
        RuleFor(c => c.JobTitle).MaximumLength(150);
    }
}
