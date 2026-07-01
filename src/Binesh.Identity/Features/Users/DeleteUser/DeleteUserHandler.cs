using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.DeleteUser;

public sealed class DeleteUserHandler(UserManager<User> userManager)
    : IRequestHandler<DeleteUserCommand, Unit>
{
    public async Task<Unit> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        if (request.Id == request.RequesterId)
        {
            throw new ForbiddenException("You cannot delete your own account.");
        }

        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        if (await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
        {
            throw new ForbiddenException("SuperAdmin accounts cannot be deleted.");
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(
                string.Join("; ", result.Errors.Select(e => e.Description)),
                "user.delete_failed");
        }

        return Unit.Value;
    }
}
