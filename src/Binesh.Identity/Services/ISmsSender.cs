namespace Binesh.Identity.Services;

public interface ISmsSender
{
    /// <summary>Sends an OTP code to the given phone number. Returns true on success.</summary>
    Task<bool> SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken);
}
