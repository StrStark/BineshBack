using System.Security.Cryptography;
using System.Text;

namespace Binesh.Domain.Identity;

/// <summary>
/// One row per refresh-token issuance. Tokens are stored as SHA-256 hashes —
/// the raw token leaves the server only once (in the response) and is never
/// persisted.
///
/// Rotation: every refresh produces a new RefreshToken row with
/// <see cref="ReplacedByTokenId"/> pointing back to the chain. Reuse of a
/// token that has <see cref="UsedAt"/> set means the chain is compromised;
/// the handler revokes the entire <see cref="UserSession"/> as defence.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public UserSession Session { get; private set; } = default!;

    public string TokenHash { get; private set; } = default!;

    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was redeemed. Null = never used.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Id of the token issued in exchange for this one (chain link).</summary>
    public Guid? ReplacedByTokenId { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public bool IsActive =>
        UsedAt is null && RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;

    // EF Core
    private RefreshToken() { }

    /// <summary>
    /// Issues a new refresh token. Returns the raw value (return to client)
    /// and the persisted entity (save to DB) so the raw value never lives on
    /// the entity.
    /// </summary>
    public static (string RawToken, RefreshToken Entity) Issue(
        Guid sessionId,
        TimeSpan lifetime)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            TokenHash = Hash(raw),
            IssuedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(lifetime),
        };
        return (raw, entity);
    }

    public void MarkUsed(Guid replacedByTokenId)
    {
        UsedAt = DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }

    public void Revoke(string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }
        RevokedAt = DateTimeOffset.UtcNow;
        RevocationReason = reason;
    }

    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
