using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.GetMyProfile;

public sealed record GetMyProfileQuery(Guid UserId) : IRequest<UserDto>;
