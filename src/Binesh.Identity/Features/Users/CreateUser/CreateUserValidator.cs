using Binesh.Identity.Services;
using FluentValidation;

namespace Binesh.Identity.Features.Users.CreateUser;

public sealed class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(c => c.PhoneNumber)
            .NotEmpty()
            .Must(PhoneNumberNormalizer.IsValid)
            .WithMessage("Phone number must be a valid Iranian mobile number.");

        RuleFor(c => c.FirstName).MaximumLength(100);
        RuleFor(c => c.LastName).MaximumLength(100);
        RuleFor(c => c.JobTitle).MaximumLength(150);
    }
}
