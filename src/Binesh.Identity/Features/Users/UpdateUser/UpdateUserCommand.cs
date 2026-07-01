using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.UpdateUser;

/// <summary>
/// SuperAdmin updates another user's profile (excluding their own — they use
/// /users/me for that). Role changes are NOT supported until the role model
/// expands beyond {SuperAdmin, Admin}.
/// </summary>
public sealed record UpdateUserCommand(
    Guid Id,
    string? FirstName,
    string? LastName,
    string? JobTitle,
    DateTimeOffset? BirthDate)
    : IRequest<UserDto>;
