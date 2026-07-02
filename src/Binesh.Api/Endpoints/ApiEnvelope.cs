namespace Binesh.Api.Endpoints;

internal static class ApiEnvelope
{
    public static object Success<T>(T? body = default, string message = "success", int code = StatusCodes.Status200OK) =>
        new { code, status = "success", message, body };

    public static object Error(string message, int code) =>
        new { code, status = "error", message };
}
