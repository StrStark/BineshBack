namespace Binesh.Identity.Services;

/// <summary>
/// Normalizes Iranian mobile numbers to the canonical E.164 form (+98XXXXXXXXXX).
/// Replaces the old code's "use whatever the user typed" approach which made
/// duplicate-account detection impossible (e.g. 09123456789 vs +989123456789
/// were treated as different users).
/// </summary>
public static class PhoneNumberNormalizer
{
    public static string? Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = new string([.. phone.Where(c => char.IsDigit(c) || c == '+')]);

        // 09XXXXXXXXX -> +989XXXXXXXXX
        if (trimmed.StartsWith("09") && trimmed.Length == 11)
        {
            return "+98" + trimmed[1..];
        }

        // 9XXXXXXXXX (10 digits, no leading 0) -> +989XXXXXXXXX
        if (trimmed.StartsWith('9') && trimmed.Length == 10)
        {
            return "+98" + trimmed;
        }

        // 98XXXXXXXXXX (no plus) -> +98XXXXXXXXXX
        if (trimmed.StartsWith("98") && trimmed.Length == 12)
        {
            return "+" + trimmed;
        }

        // Already canonical
        if (trimmed.StartsWith("+98") && trimmed.Length == 13)
        {
            return trimmed;
        }

        return null;
    }

    public static bool IsValid(string? phone) => Normalize(phone) is not null;
}
