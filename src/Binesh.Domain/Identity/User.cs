using Microsoft.AspNetCore.Identity;

namespace Binesh.Domain.Identity;

/// <summary>
/// Application user. Inherits from <see cref="IdentityUser{TKey}"/> for the
/// well-known Identity columns (UserName, PhoneNumber, lockout, etc.).
///
/// Phone-only auth flow — most users have <c>UserName == PhoneNumber</c> and no email.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    public User()
    {
        Id = Guid.NewGuid();
    }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? JobTitle { get; set; }

    public DateTimeOffset? BirthDate { get; set; }
    public string? ProfileImageName { get; set; }

    /// <summary>Last time an OTP was requested. Used to enforce resend delay.</summary>
    public DateTimeOffset? LastOtpRequestedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Active + revoked sessions for this user.</summary>
    public List<UserSession> Sessions { get; set; } = [];

    public string? FullName =>
        string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName)
            ? null
            : $"{FirstName} {LastName}".Trim();
}
