using Binesh.Domain.Identity;

namespace Binesh.Identity.Services;

public interface IOtpService
{
    /// <summary>Generates a phone-confirmation OTP for the user. Returns the 6-digit code.</summary>
    Task<string> GenerateAsync(User user);

    /// <summary>Validates the OTP. Returns true if valid (and consumes it).</summary>
    Task<bool> VerifyAsync(User user, string otp);
}
