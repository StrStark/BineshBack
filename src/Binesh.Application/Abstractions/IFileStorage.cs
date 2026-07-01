namespace Binesh.Application.Abstractions;

/// <summary>
/// Thin abstraction over an S3-style object store. The Infrastructure layer
/// implements this against MinIO (or any S3-compatible backend); handlers
/// depend on the interface so business logic stays free of SDK types.
///
/// <para>The contract is intentionally narrow: clients upload directly to
/// pre-signed URLs (no bytes flow through our API process), the server
/// records the resulting object key, and downloads are served via short-lived
/// pre-signed GETs. The server never proxies object bodies.</para>
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Builds a pre-signed URL the client uploads directly to. The returned
    /// <see cref="PresignedUploadUrl"/> includes any required headers (e.g.
    /// <c>Content-Type</c>) that the client MUST replay verbatim — MinIO
    /// rejects the upload if any signed header is missing or different.
    /// </summary>
    Task<PresignedUploadUrl> CreatePresignedUploadAsync(
        string objectKey,
        string contentType,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    /// <summary>Short-lived pre-signed GET. The URL is the only thing the client needs to fetch the object.</summary>
    Task<Uri> CreatePresignedDownloadAsync(
        string objectKey,
        TimeSpan ttl,
        CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken);

    Task DeleteAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record PresignedUploadUrl(
    string ObjectKey,
    Uri Url,
    string Method,
    DateTimeOffset ExpiresAt,
    IReadOnlyDictionary<string, string> RequiredHeaders);
