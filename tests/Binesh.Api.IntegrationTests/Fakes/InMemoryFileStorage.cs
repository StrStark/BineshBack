using System.Collections.Concurrent;
using Binesh.Application.Abstractions;

namespace Binesh.Api.IntegrationTests.Fakes;

/// <summary>
/// Test double for <see cref="IFileStorage"/>. Records every call and
/// returns deterministic pre-signed URLs so tests can assert against them
/// without spinning up MinIO. <see cref="MarkUploaded"/> lets a test
/// simulate "the client finished its PUT".
/// </summary>
public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, bool> _uploaded = new();
    public List<string> UploadUrlRequests { get; } = [];
    public List<string> DownloadUrlRequests { get; } = [];
    public List<string> DeletedKeys { get; } = [];

    /// <summary>Pretend the client successfully PUT to the URL for <paramref name="objectKey"/>.</summary>
    public void MarkUploaded(string objectKey) => _uploaded[objectKey] = true;

    public void Reset()
    {
        _uploaded.Clear();
        UploadUrlRequests.Clear();
        DownloadUrlRequests.Clear();
        DeletedKeys.Clear();
    }

    public Task<PresignedUploadUrl> CreatePresignedUploadAsync(
        string objectKey, string contentType, TimeSpan ttl, CancellationToken cancellationToken)
    {
        UploadUrlRequests.Add(objectKey);
        var url = new Uri($"https://fake-minio.test/upload/{Uri.EscapeDataString(objectKey)}");
        var headers = new Dictionary<string, string> { ["Content-Type"] = contentType };
        return Task.FromResult(new PresignedUploadUrl(
            objectKey, url, "PUT", DateTimeOffset.UtcNow.Add(ttl), headers));
    }

    public Task<Uri> CreatePresignedDownloadAsync(
        string objectKey, TimeSpan ttl, CancellationToken cancellationToken)
    {
        DownloadUrlRequests.Add(objectKey);
        return Task.FromResult(new Uri($"https://fake-minio.test/get/{Uri.EscapeDataString(objectKey)}?ttl={(int)ttl.TotalSeconds}"));
    }

    public Task<bool> ExistsAsync(string objectKey, CancellationToken cancellationToken) =>
        Task.FromResult(_uploaded.ContainsKey(objectKey));

    public Task DeleteAsync(string objectKey, CancellationToken cancellationToken)
    {
        _uploaded.TryRemove(objectKey, out _);
        DeletedKeys.Add(objectKey);
        return Task.CompletedTask;
    }
}
