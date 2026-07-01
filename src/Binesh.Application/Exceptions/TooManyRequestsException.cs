namespace Binesh.Application.Exceptions;

public sealed class TooManyRequestsException(string message, TimeSpan? retryAfter = null)
    : AppException(message)
{
    public override int StatusCode => 429;
    public override string ErrorCode => "rate.exceeded";

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
