using Binesh.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;

namespace Binesh.Infrastructure.Ai;

internal sealed class DataProtectionAiSettingsProtector(IDataProtectionProvider provider) : IAiSettingsProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("Binesh.UserAiSettings.ApiKey.v1");

    public string Protect(string value) => _protector.Protect(value);

    public string Unprotect(string protectedValue) => _protector.Unprotect(protectedValue);
}
