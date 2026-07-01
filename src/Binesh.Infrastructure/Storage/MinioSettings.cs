using System.ComponentModel.DataAnnotations;

namespace Binesh.Infrastructure.Storage;

/// <summary>
/// Binds to <c>Minio:*</c> configuration. Defaults match the dev
/// <c>docker-compose.yml</c> MinIO container; in prod every value must be
/// overridden via env vars.
/// </summary>
public sealed class MinioSettings
{
    public const string SectionName = "Minio";

    [Required] public string Endpoint { get; set; } = "minio:9000";
    [Required] public string AccessKey { get; set; } = default!;
    [Required] public string SecretKey { get; set; } = default!;
    [Required] public string BucketName { get; set; } = "binesh";
    public bool UseSsl { get; set; }

    /// <summary>
    /// Public-facing endpoint the browser will see in the pre-signed URL.
    /// Defaults to <see cref="Endpoint"/> when blank — set this in prod
    /// when MinIO sits behind a reverse proxy on a different hostname.
    /// </summary>
    public string? PublicEndpoint { get; set; }
}
