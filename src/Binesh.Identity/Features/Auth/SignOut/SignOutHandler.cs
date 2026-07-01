using Binesh.Application.Abstractions;
using Binesh.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Auth.SignOut;

/// <summary>
/// Revokes the session backing the given refresh token. Silently succeeds for
/// unknown tokens — don't tell the caller whether the token was valid.
/// </summary>
public sealed class SignOutHandler(IBineshDbContext db)
    : IRequestHandler<SignOutCommand, Unit>
{
    public async Task<Unit> Handle(SignOutCommand request, CancellationToken cancellationToken)
    {
        var hash = RefreshToken.Hash(request.RefreshToken);

        var token = await db.RefreshTokens
            .Include(t => t.Session)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (token is null)
        {
            return Unit.Value;
        }

        token.Session.Revoke("User signed out");
        foreach (var t in await db.RefreshTokens
            .Where(t => t.SessionId == token.SessionId && t.RevokedAt == null)
            .ToListAsync(cancellationToken))
        {
            t.Revoke("Session signed out");
        }

        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
