using MediatR;

namespace Binesh.Identity.Features.Auth.VerifyOtp;

public sealed record VerifyOtpCommand(
    string PhoneNumber,
    string Otp,
    string? DeviceInfo,
    string? UserAgent,
    string? IpAddress)
    : IRequest<VerifyOtpResponse>;

public sealed record VerifyOtpResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
