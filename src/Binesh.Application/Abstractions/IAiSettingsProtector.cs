namespace Binesh.Application.Abstractions;

public interface IAiSettingsProtector
{
    string Protect(string value);
    string Unprotect(string protectedValue);
}
