using System.ComponentModel.DataAnnotations;

namespace Binesh.Identity.Configuration;

public sealed class SmsSettings
{
    public const string SectionName = "Sms";

    /// <summary>Which SMS sender to use. Values: "ippanel" (prod) or "log" (dev — writes OTP to logs).</summary>
    [Required]
    public string Provider { get; set; } = "log";

    public IppanelSettings Ippanel { get; set; } = new();

    public sealed class IppanelSettings
    {
        public string BaseUrl { get; set; } = "https://edge.ippanel.com/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string FromPhoneNumber { get; set; } = string.Empty;
        public string PatternCode { get; set; } = string.Empty;
    }
}
