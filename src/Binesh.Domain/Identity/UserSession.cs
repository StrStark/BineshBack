namespace Binesh.Domain.Identity;

/// <summary>
/// One row per (user, device) login. Tracks the device context and groups all
/// refresh tokens that belong to the same login chain (the "family" for
/// reuse-detection purposes — see <see cref="RefreshToken"/>).
/// </summary>
public sealed class UserSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = default!;

    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevocationReason { get; private set; }

    public List<RefreshToken> RefreshTokens { get; private set; } = [];

    public bool IsActive => RevokedAt is null;

    // EF Core
    private UserSession() { }

    public static UserSession Start(
        Guid userId,
        string? deviceInfo,
        string? ipAddress,
        string? userAgent)
    {
        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            StartedAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
    }

    public void Touch() => LastSeenAt = DateTimeOffset.UtcNow;

    public void Revoke(string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }
        RevokedAt = DateTimeOffset.UtcNow;
        RevocationReason = reason;
    }
}
