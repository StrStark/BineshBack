using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Features.Users.Shared;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Identity.Features.Users.GetMyProfile;

public sealed class GetMyProfileHandler(UserManager<User> userManager, IFileStorage storage)
    : IRequestHandler<GetMyProfileQuery, UserDto>
{
    private static readonly TimeSpan ImageUrlTtl = TimeSpan.FromMinutes(5);

    public async Task<UserDto> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await userManager.Users
            .SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException("User", request.UserId);

        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? string.Empty;

        string? imageUrl = null;
        if (!string.IsNullOrEmpty(user.ProfileImageName))
        {
            var uri = await storage.CreatePresignedDownloadAsync(
                user.ProfileImageName, ImageUrlTtl, cancellationToken);
            imageUrl = uri.ToString();
        }

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
            user.CreatedAt,
            imageUrl);
    }
}
