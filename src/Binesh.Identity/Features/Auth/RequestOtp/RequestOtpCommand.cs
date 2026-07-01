using MediatR;

namespace Binesh.Identity.Features.Auth.RequestOtp;

/// <summary>
/// Requests an OTP for a phone number. Returns 200 unconditionally so an
/// attacker can't enumerate registered users by probing this endpoint.
/// </summary>
public sealed record RequestOtpCommand(string PhoneNumber) : IRequest<Unit>;
