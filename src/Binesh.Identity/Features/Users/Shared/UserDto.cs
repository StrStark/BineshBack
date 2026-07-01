namespace Binesh.Identity.Features.Users.Shared;

/// <summary>
/// Public projection of a user. Used by every Users/* slice as response or
/// list-item shape.
/// </summary>
public sealed record UserDto(
    Guid Id,
    string PhoneNumber,
    string? FirstName,
    string? LastName,
    string? JobTitle,
    DateTimeOffset? BirthDate,
    string? ProfileImageName,
    string Role,
    bool PhoneNumberConfirmed,
    DateTimeOffset CreatedAt,
    /// <summary>
    /// Round 15 — short-lived pre-signed GET URL for the profile image.
    /// Populated only on the <c>GET /api/users/me</c> response (where the UI
    /// is about to render the image); list / admin endpoints leave it
    /// <c>null</c> so each query doesn't pay the storage round-trip per row.
    /// </summary>
    string? ProfileImageUrl = null);
