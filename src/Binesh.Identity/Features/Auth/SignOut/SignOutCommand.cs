using MediatR;

namespace Binesh.Identity.Features.Auth.SignOut;

public sealed record SignOutCommand(string RefreshToken) : IRequest<Unit>;
