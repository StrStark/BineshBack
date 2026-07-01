namespace Binesh.Application.Exceptions;

public sealed class UnauthorizedException(string message = "Authentication is required.")
    : AppException(message)
{
    public override int StatusCode => 401;
    public override string ErrorCode => "auth.unauthorized";
}
