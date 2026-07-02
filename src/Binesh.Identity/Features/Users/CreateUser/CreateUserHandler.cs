using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Authorization;
using Binesh.Identity.Features.Users.Shared;
using Binesh.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.CreateUser;

public sealed class CreateUserHandler(UserManager<User> userManager)
    : IRequestHandler<CreateUserCommand, UserDto>
{
    public async Task<UserDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var phone = PhoneNumberNormalizer.Normalize(request.PhoneNumber)!;

        var duplicate = await userManager.Users
            .AnyAsync(u => u.PhoneNumber == phone, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                $"A user with phone {phone} already exists.",
                "user.duplicate_phone");
        }

        var user = new User
        {
            UserName = phone,
            PhoneNumber = phone,
            PhoneNumberConfirmed = false,    // they confirm on first OTP verify
            FirstName = request.FirstName,
            LastName = request.LastName,
            JobTitle = request.JobTitle,
        };

        var create = await userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            throw new ConflictException(
                string.Join("; ", create.Errors.Select(e => e.Description)),
                "user.create_failed");
        }

        var addRole = await userManager.AddToRoleAsync(user, AppRoles.Admin);
        if (!addRole.Succeeded)
        {
            // Roll back the user if role assignment fails so we don't leave an
            // orphan account.
            await userManager.DeleteAsync(user);
            throw new ConflictException(
                string.Join("; ", addRole.Errors.Select(e => e.Description)),
                "user.role_assignment_failed");
        }

        return new UserDto(
            user.Id,
            user.PhoneNumber!,
            user.FirstName,
            user.LastName,
            user.JobTitle,
            user.BirthDate,
            user.ProfileImageName,
            AppRoles.Admin,
            user.PhoneNumberConfirmed,
            user.CreatedAt,
            CompanyId: user.CompanyId);
    }
}
