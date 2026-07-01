using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Users.Shared;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.UpdateUser;

public sealed class UpdateUserHandler(UserManager<User> userManager)
    : IRequestHandler<UpdateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

        // SuperAdmin accounts are immutable via this endpoint — they edit
        // themselves via /users/me only.
        if (await userManager.IsInRoleAsync(user, AppRoles.SuperAdmin))
        {
            throw new ForbiddenException("SuperAdmin accounts cannot be modified through this endpoint.");
        }

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.JobTitle = request.JobTitle ?? user.JobTitle;
        user.BirthDate = request.BirthDate ?? user.BirthDate;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            throw new ConflictException(
                string.Join("; ", result.Errors.Select(e => e.Description)),
                "user.update_failed");
        }

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;
        return new UserDto(
            user.Id,
            user.PhoneNumber ?? string.Empty,
            user.FirstName,
            user.LastName,
            user.JobTitle,
            user.BirthDate,
            user.ProfileImageName,
            role,
            user.PhoneNumberConfirmed,
            user.CreatedAt);
    }
}
