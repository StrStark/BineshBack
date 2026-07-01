using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Configuration;
using Binesh.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Binesh.Identity.Features.Auth.RequestOtp;

/// <summary>
/// Sends an OTP to a registered phone number. Registration is closed — only
/// users created in advance by a SuperAdmin (via /api/users) can receive OTPs.
///
/// The endpoint returns 200 even for unknown phones so an attacker cannot
/// enumerate registered accounts by probing it.
/// </summary>
public sealed class RequestOtpHandler(
    UserManager<User> userManager,
    IOtpService otpService,
    ISmsSender smsSender,
    IOptions<IdentitySettings> identityOptions,
    ILogger<RequestOtpHandler> logger)
    : IRequestHandler<RequestOtpCommand, Unit>
{
    public async Task<Unit> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.PhoneNumber)!;

        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.PhoneNumber == phone, cancellationToken);

        if (user is null)
        {
            // Don't reveal "unknown user" to the caller — log it server-side
            // so abuse / probing shows up in monitoring.
            logger.LogInformation(
                "OTP requested for unknown phone {Phone} — silently dropped.", phone);
            return Unit.Value;
        }

        // Enforce resend delay.
        var resendDelay = identityOptions.Value.Otp.ResendDelay;
        if (user.LastOtpRequestedAt is { } last
            && DateTimeOffset.UtcNow - last < resendDelay)
        {
            var waitFor = resendDelay - (DateTimeOffset.UtcNow - last);
            throw new TooManyRequestsException(
                $"Please wait {(int)waitFor.TotalSeconds}s before requesting another code.",
                waitFor);
        }

        var otp = await otpService.GenerateAsync(user);
        user.LastOtpRequestedAt = DateTimeOffset.UtcNow;
        await userManager.UpdateAsync(user);

        var sent = await smsSender.SendOtpAsync(phone, otp, cancellationToken);
        if (!sent)
        {
            logger.LogError("Failed to send OTP SMS to {Phone}", phone);
        }

        return Unit.Value;
    }
}
