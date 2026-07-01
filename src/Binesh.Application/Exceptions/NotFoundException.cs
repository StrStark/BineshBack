namespace Binesh.Application.Exceptions;

public sealed class NotFoundException(string resource, object key)
    : AppException($"{resource} with key '{key}' was not found.")
{
    public override int StatusCode => 404;
    public override string ErrorCode => "resource.not_found";

    public string Resource { get; } = resource;
    public object Key { get; } = key;
}
