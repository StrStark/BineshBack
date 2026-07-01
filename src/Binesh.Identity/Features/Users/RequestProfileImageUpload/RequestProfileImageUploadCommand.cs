using Binesh.Application.Abstractions;
using MediatR;

namespace Binesh.Identity.Features.Users.RequestProfileImageUpload;

/// <summary>
/// Returns a pre-signed PUT URL the caller uploads directly to MinIO. The
/// returned <see cref="PresignedUploadUrl.ObjectKey"/> is what the caller
/// then posts back via <see cref="SetProfileImage.SetProfileImageCommand"/>
/// once the upload completes.
/// </summary>
public sealed record RequestProfileImageUploadCommand(Guid UserId, string ContentType)
    : IRequest<PresignedUploadUrl>;
