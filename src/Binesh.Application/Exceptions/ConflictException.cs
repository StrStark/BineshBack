namespace Binesh.Application.Exceptions;

public sealed class ConflictException(string message, string? errorCode = null)
    : AppException(message)
{
    public override int StatusCode => 409;
    public override string ErrorCode { get; } = errorCode ?? "resource.conflict";
}
