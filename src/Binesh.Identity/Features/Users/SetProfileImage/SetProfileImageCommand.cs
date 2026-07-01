using Binesh.Identity.Features.Users.Shared;
using MediatR;

namespace Binesh.Identity.Features.Users.SetProfileImage;

/// <summary>
/// Records the uploaded object key on the user. Pass <c>null</c> or empty
/// for <see cref="ObjectKey"/> to clear the image. The handler verifies the
/// object exists in storage before recording — a leaked URL pointing at an
/// unrelated key can't poison a user's profile.
/// </summary>
public sealed record SetProfileImageCommand(Guid UserId, string? ObjectKey) : IRequest<UserDto>;
