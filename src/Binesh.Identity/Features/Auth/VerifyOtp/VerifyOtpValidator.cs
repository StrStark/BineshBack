using Binesh.Identity.Services;
using FluentValidation;

namespace Binesh.Identity.Features.Auth.VerifyOtp;

public sealed class VerifyOtpValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpValidator()
    {
        RuleFor(c => c.PhoneNumber)
            .NotEmpty()
            .Must(PhoneNumberNormalizer.IsValid)
            .WithMessage("Phone number must be a valid Iranian mobile number.");

        RuleFor(c => c.Otp)
            .NotEmpty()
            .Length(4, 8)
            .Matches(@"^\d+$").WithMessage("OTP must be digits only.");
    }
}
