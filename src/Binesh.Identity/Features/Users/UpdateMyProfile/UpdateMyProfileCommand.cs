using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    Guid UserId,
    string? FirstName,
    string? LastName,
    string? JobTitle,
    DateTimeOffset? BirthDate)
    : IRequest<UserDto>;
