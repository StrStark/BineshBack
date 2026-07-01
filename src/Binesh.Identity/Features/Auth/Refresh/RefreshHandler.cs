using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Configuration;
using Binesh.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Binesh.Identity.Features.Auth.Refresh;

/// <summary>
/// Rotates refresh tokens with reuse detection.
///
/// Flow:
///   1. Find the RefreshToken row by hash.
///   2. If revoked / used → the chain is compromised. Revoke the entire session
///      and refuse. This is the "reuse detection" the old code did not have.
///   3. If expired → 401.
///   4. Otherwise: mark used, issue a new token in the same chain.
///
/// The raw token never leaves the server except in the response.
/// </summary>
public sealed class RefreshHandler(
    IBineshDbContext db,
    UserManager<User> userManager,
    IJwtTokenService jwtTokenService,
    IOptions<JwtSettings> jwtOptions,
    ILogger<RefreshHandler> logger)
    : IRequestHandler<RefreshCommand, RefreshResponse>
{
    public async Task<RefreshResponse> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var hash = RefreshToken.Hash(request.RefreshToken);

        var existing = await db.RefreshTokens
            .Include(t => t.Session)
            .ThenInclude(s => s.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        // Reuse detection: if a previously-used or revoked token is presented,
        // we assume the client is compromised and burn the whole session.
        if (existing.UsedAt is not null || existing.RevokedAt is not null)
        {
            existing.Session.Revoke("Refresh-token reuse detected");
            foreach (var t in await db.RefreshTokens
                .Where(t => t.SessionId == existing.SessionId && t.RevokedAt == null)
                .ToListAsync(cancellationToken))
            {
                t.Revoke("Session revoked due to reuse detection");
            }
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Refresh-token reuse detected for session {SessionId}; revoked all tokens.",
                existing.SessionId);

            throw new UnauthorizedException("Refresh token has already been used.");
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        if (!existing.Session.IsActive)
        {
            throw new UnauthorizedException("Session is no longer active.");
        }

        // Rotate.
        var jwt = jwtOptions.Value;
        var (raw, next) = RefreshToken.Issue(existing.SessionId, jwt.RefreshTokenLifetime);
        db.RefreshTokens.Add(next);
        existing.MarkUsed(next.Id);
        existing.Session.Touch();

        await db.SaveChangesAsync(cancellationToken);

        var roles = await userManager.GetRolesAsync(existing.Session.User);
        var accessToken = jwtTokenService.IssueAccessToken(existing.Session.User, roles);

        return new RefreshResponse(
            AccessToken: accessToken,
            RefreshToken: raw,
            AccessTokenExpiresAt: DateTime.UtcNow.Add(jwt.AccessTokenLifetime),
            RefreshTokenExpiresAt: next.ExpiresAt.UtcDateTime);
    }
}
