using Binesh.Identity.Services;
using FluentValidation;

namespace Binesh.Identity.Features.Auth.RequestOtp;

public sealed class RequestOtpValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpValidator()
    {
        RuleFor(c => c.PhoneNumber)
            .NotEmpty()
            .Must(PhoneNumberNormalizer.IsValid)
            .WithMessage("Phone number must be a valid Iranian mobile number.");
    }
}
