using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Features.Users.Shared;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.GetUserById;

public sealed class GetUserByIdHandler(UserManager<User> userManager)
    : IRequestHandler<GetUserByIdQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("User", request.Id);

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
