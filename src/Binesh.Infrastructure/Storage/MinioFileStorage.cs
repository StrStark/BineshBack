using Binesh.Application.Abstractions;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Binesh.Infrastructure.Storage;

/// <summary>
/// MinIO-backed <see cref="IFileStorage"/>. All operations go through MinIO's
/// pre-signed URL surface — the API process never streams bytes itself.
/// </summary>
public sealed class MinioFileStorage(IMinioClient client, IOptions<MinioSettings> settings) : IFileStorage
{
    private readonly IMinioClient _client = client;
    private readonly MinioSettings _settings = settings.Value;

    public async Task<PresignedUploadUrl> CreatePresignedUploadAsync(
        string objectKey, string contentType, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var args = new PresignedPutObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectKey)
            .WithExpiry((int)ttl.TotalSeconds);

        var url = await _client.PresignedPutObjectAsync(args);

        // The client MUST replay Content-Type — MinIO's signature is bound to it.
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = contentType,
        };

        return new PresignedUploadUrl(
            objectKey,
            new Uri(url),
            "PUT",
            DateTimeOffset.UtcNow.Add(ttl),
            headers);
    }

    public async Task<Uri> CreatePresignedDownloadAsync(
        string objectKey, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_settings.BucketName)
            .WithObject(objectKey)
            .WithExpiry((int)ttl.TotalSeconds);

        var url = await _client.PresignedGetObjectAsync(args);
        return new Uri(url);
    }

    public async Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken)
    {
        try
        {
            await _client.StatObjectAsync(
                new StatObjectArgs()
                    .WithBucket(_settings.BucketName)
                    .WithObject(objectKey),
                cancellationToken);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return false;
        }
    }

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken) =>
        _client.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(_settings.BucketName)
                .WithObject(objectKey),
            cancellationToken);
}
