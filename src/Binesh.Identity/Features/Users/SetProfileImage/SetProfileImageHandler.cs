using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Domain.Identity;
using Binesh.Identity.Features.Users.Shared;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace Binesh.Identity.Features.Users.SetProfileImage;

public sealed class SetProfileImageHandler(
    UserManager<User> userManager,
    IFileStorage storage)
    : IRequestHandler<SetProfileImageCommand, UserDto>
{
    public async Task<UserDto> Handle(SetProfileImageCommand request, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new NotFoundException("User", request.UserId);

        var newKey = string.IsNullOrEmpty(request.ObjectKey) ? null : request.ObjectKey;

        if (newKey is not null)
        {
            // The presigned URL has the user's id baked into the prefix
            // (RequestProfileImageUploadHandler); refuse keys outside the
            // caller's own namespace so a leaked URL of one user can't be
            // stuffed into another user's profile via this endpoint.
            var requiredPrefix = $"profile-images/{request.UserId:N}/";
            if (!newKey.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                throw new ValidationException(
                    [new ValidationFailure("objectKey", "ObjectKey does not belong to the caller.")]);
            }

            if (!await storage.ExistsAsync(newKey, cancellationToken))
            {
                throw new ValidationException(
                    [new ValidationFailure("objectKey", "No upload has been completed at that key.")]);
            }
        }

        var oldKey = user.ProfileImageName;
        user.ProfileImageName = newKey;
        var update = await userManager.UpdateAsync(user);
        if (!update.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", update.Errors.Select(e => e.Description)));
        }

        // Best-effort cleanup of the prior object; failure is logged downstream.
        if (!string.IsNullOrEmpty(oldKey) && oldKey != newKey)
        {
            try { await storage.DeleteAsync(oldKey, cancellationToken); } catch { /* swallow */ }
        }

        var roles = await userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? string.Empty;
        return new UserDto(
            user.Id, user.PhoneNumber ?? string.Empty, user.FirstName, user.LastName,
            user.JobTitle, user.BirthDate, user.ProfileImageName, role,
            user.PhoneNumberConfirmed, user.CreatedAt);
    }
}
