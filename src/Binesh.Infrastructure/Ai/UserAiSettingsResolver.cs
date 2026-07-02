using Binesh.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Infrastructure.Ai;

internal sealed class UserAiSettingsResolver(
    IBineshDbContext db,
    IAiSettingsProtector protector) : IUserAiSettingsResolver
{
    public async Task<UserAiRuntimeSettings?> ResolveAsync(Guid userId, CancellationToken cancellationToken)
    {
        var settings = await db.UserAiSettings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings?.ApiKeyEncrypted is null)
        {
            return null;
        }

        return new UserAiRuntimeSettings(
            protector.Unprotect(settings.ApiKeyEncrypted),
            settings.Model,
            settings.BaseUrl);
    }
}
