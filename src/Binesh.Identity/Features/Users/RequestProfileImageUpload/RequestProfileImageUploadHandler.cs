using Binesh.Application.Abstractions;
using MediatR;

namespace Binesh.Identity.Features.Users.RequestProfileImageUpload;

public sealed class RequestProfileImageUploadHandler(IFileStorage storage)
    : IRequestHandler<RequestProfileImageUploadCommand, PresignedUploadUrl>
{
    private static readonly TimeSpan UploadTtl = TimeSpan.FromMinutes(10);

    public Task<PresignedUploadUrl> Handle(RequestProfileImageUploadCommand request, CancellationToken cancellationToken)
    {
        // Namespace by user id so a leaked URL only ever overwrites that user's slot.
        var extension = request.ContentType.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/webp" => "webp",
            _ => "jpg",
        };
        var objectKey = $"profile-images/{request.UserId:N}/{Guid.NewGuid():N}.{extension}";
        return storage.CreatePresignedUploadAsync(objectKey, request.ContentType, UploadTtl, cancellationToken);
    }
}
