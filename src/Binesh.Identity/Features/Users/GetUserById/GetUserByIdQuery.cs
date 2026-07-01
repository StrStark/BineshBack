using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid Id) : IRequest<UserDto>;
