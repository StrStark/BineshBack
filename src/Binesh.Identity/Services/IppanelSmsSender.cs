using System.Text;
using System.Text.Json;
using Binesh.Identity.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Binesh.Identity.Services;

/// <summary>
/// IPPanel pattern-SMS sender. Port of the old SmsService, minus:
///   - logging the OTP value itself (security fix),
///   - silent failure when not configured (now throws at startup via ValidateOnStart).
/// </summary>
internal sealed class IppanelSmsSender(
    IHttpClientFactory httpClientFactory,
    IOptions<SmsSettings> options,
    ILogger<IppanelSmsSender> logger) : ISmsSender
{
    private static readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<bool> SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken)
    {
        var settings = options.Value.Ippanel;

        var client = httpClientFactory.CreateClient(nameof(IppanelSmsSender));
        client.BaseAddress = new Uri(settings.BaseUrl);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", settings.ApiKey);

        var body = new
        {
            sending_type = "pattern",
            from_number = settings.FromPhoneNumber,
            code = settings.PatternCode,
            recipients = new[] { phoneNumber },
            @params = new { OTP = otp },
        };

        using var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/v1/api/send", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "IPPanel SMS send failed for {Phone}. Status={Status} Body={Body}",
                phoneNumber, (int)response.StatusCode, error);
            return false;
        }

        return true;
    }
}
