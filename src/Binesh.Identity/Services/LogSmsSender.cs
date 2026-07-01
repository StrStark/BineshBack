using Microsoft.Extensions.Logging;

namespace Binesh.Identity.Services;

/// <summary>
/// Dev SMS sender — writes the OTP to logs instead of sending. NEVER use in prod.
/// The old code's SmsService used to log the OTP value even with a real provider;
/// that was a security bug. Now you have to pick this provider explicitly.
/// </summary>
internal sealed class LogSmsSender(ILogger<LogSmsSender> logger) : ISmsSender
{
    public Task<bool> SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken)
    {
        logger.LogWarning(
            "[DEV] OTP for {Phone}: {Otp}  — switch Sms:Provider to 'ippanel' for real SMS.",
            phoneNumber, otp);
        return Task.FromResult(true);
    }
}
