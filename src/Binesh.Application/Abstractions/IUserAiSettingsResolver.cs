namespace Binesh.Application.Abstractions;

public interface IUserAiSettingsResolver
{
    Task<UserAiRuntimeSettings?> ResolveAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record UserAiRuntimeSettings(
    string ApiKey,
    string? Model,
    string? BaseUrl);
