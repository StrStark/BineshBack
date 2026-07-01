using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Binesh.Infrastructure.Storage;

/// <summary>
/// Idempotent startup hook that ensures the configured bucket exists. Logs
/// and continues on failure so the API still boots when MinIO is briefly
/// unavailable; subsequent storage calls will surface the real error.
/// </summary>
internal sealed class MinioBootstrapService(
    IMinioClient client,
    IOptions<MinioSettings> settings,
    ILogger<MinioBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucket = settings.Value.BucketName;
        try
        {
            var exists = await client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(bucket), cancellationToken);
            if (!exists)
            {
                await client.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(bucket), cancellationToken);
                logger.LogInformation("Created MinIO bucket '{Bucket}'.", bucket);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to verify MinIO bucket '{Bucket}' at startup; storage calls may fail.", bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
