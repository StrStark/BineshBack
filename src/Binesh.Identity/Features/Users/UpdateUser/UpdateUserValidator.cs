using FluentValidation;

namespace Binesh.Identity.Features.Users.UpdateUser;

public sealed class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.FirstName).MaximumLength(100);
        RuleFor(c => c.LastName).MaximumLength(100);
        RuleFor(c => c.JobTitle).MaximumLength(150);
    }
}
