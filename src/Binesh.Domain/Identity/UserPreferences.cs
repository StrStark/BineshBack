namespace Binesh.Domain.Identity;

public sealed class UserPreferences
{
    public Guid UserId { get; set; }
    public string PreferencesJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
