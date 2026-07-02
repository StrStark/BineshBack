namespace Binesh.Domain.Ai;

public sealed class UserAiSettings
{
    public Guid UserId { get; set; }
    public string? ApiKeyEncrypted { get; set; }
    public string? ApiKeyPreview { get; set; }
    public string? Model { get; set; }
    public string? BaseUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
