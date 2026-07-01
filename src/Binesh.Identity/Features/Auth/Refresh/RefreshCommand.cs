using MediatR;

namespace Binesh.Identity.Features.Auth.Refresh;

public sealed record RefreshCommand(string RefreshToken) : IRequest<RefreshResponse>;

public sealed record RefreshResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    DateTime RefreshTokenExpiresAt);
