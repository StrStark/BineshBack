namespace Binesh.Application.Exceptions;

public sealed class ForbiddenException(string message = "You do not have permission to perform this action.")
    : AppException(message)
{
    public override int StatusCode => 403;
    public override string ErrorCode => "auth.forbidden";
}
