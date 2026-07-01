using System.Collections.Concurrent;
using Binesh.Identity.Services;

namespace Binesh.Api.IntegrationTests.Fakes;

/// <summary>
/// Test SMS sender that captures the most recent OTP per phone number so
/// tests can read it back to drive the verify step.
/// </summary>
public sealed class CapturingSmsSender : ISmsSender
{
    private readonly ConcurrentDictionary<string, string> _otpByPhone = new();

    public string? GetLastOtp(string phone) =>
        _otpByPhone.TryGetValue(phone, out var v) ? v : null;

    public void Clear() => _otpByPhone.Clear();

    public Task<bool> SendOtpAsync(string phoneNumber, string otp, CancellationToken cancellationToken)
    {
        _otpByPhone[phoneNumber] = otp;
        return Task.FromResult(true);
    }
}
