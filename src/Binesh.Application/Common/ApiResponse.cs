using System.Net;

namespace Binesh.Application.Common;

/// <summary>
/// Legacy-compatible response envelope, ported verbatim from the old
/// <c>BineshSoloution.Dtos.ApiResponse&lt;T&gt;</c> so the frontend contract for
/// the panel endpoints is unchanged: <c>{ code, status, message, body }</c>.
///
/// The rest of the API uses raw DTOs + RFC 7807 ProblemDetails; this envelope is
/// intentionally scoped to the panel endpoints the frontend already consumes in
/// this shape.
/// </summary>
public sealed class ApiResponse<T>
{
    public HttpStatusCode Code { get; init; }
    public string Status { get; init; } = "success";
    public string Message { get; init; } = string.Empty;
    public T? Body { get; init; }

    public static ApiResponse<T> Success(string message, HttpStatusCode code, T? body = default) =>
        new() { Status = "success", Message = message, Body = body, Code = code };

    public static ApiResponse<T> Fail(string message, HttpStatusCode code, T? body = default) =>
        new() { Status = "error", Message = message, Body = body, Code = code };
}
