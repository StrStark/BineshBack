using MediatR;

namespace Binesh.Identity.Features.Users.DeleteUser;

public sealed record DeleteUserCommand(Guid Id, Guid RequesterId) : IRequest<Unit>;
