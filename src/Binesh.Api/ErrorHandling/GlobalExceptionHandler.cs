using Binesh.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Binesh.Api.ErrorHandling;

/// <summary>
/// Maps every unhandled exception to RFC 7807 ProblemDetails.
/// Replaces the 56 try/catch blocks scattered through the old controllers.
///
/// - AppException subclasses → their declared StatusCode + ErrorCode.
/// - Anything else → 500 with a generic message; full exception logged server-side.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = Build(exception, httpContext);

        // Log at the right level — known app errors are warnings, unknown are errors.
        if (exception is AppException)
        {
            logger.LogWarning(exception, "Handled application exception: {Message}", exception.Message);
        }
        else
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }

    private static ProblemDetails Build(Exception exception, HttpContext httpContext)
    {
        var traceId = httpContext.TraceIdentifier;

        if (exception is ValidationException ve)
        {
            // Put errors in Extensions rather than using ValidationProblemDetails
            // — when WriteAsJsonAsync sees the static ProblemDetails type, the
            // subclass's Errors property doesn't get serialized.
            return new ProblemDetails
            {
                Title = "Validation failed",
                Status = ve.StatusCode,
                Detail = ve.Message,
                Type = $"https://binesh.errors/{ve.ErrorCode}",
                Instance = httpContext.Request.Path,
                Extensions =
                {
                    ["traceId"] = traceId,
                    ["code"] = ve.ErrorCode,
                    ["errors"] = ve.Errors,
                },
            };
        }

        if (exception is AppException ae)
        {
            return new ProblemDetails
            {
                Title = ae.GetType().Name.Replace("Exception", string.Empty),
                Status = ae.StatusCode,
                Detail = ae.Message,
                Type = $"https://binesh.errors/{ae.ErrorCode}",
                Instance = httpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["code"] = ae.ErrorCode },
            };
        }

        // Malformed body / unparseable enum / wrong content-type → 400, not 500.
        if (exception is BadHttpRequestException badRequest)
        {
            return new ProblemDetails
            {
                Title = "Bad Request",
                Status = badRequest.StatusCode == 0 ? 400 : badRequest.StatusCode,
                Detail = badRequest.Message,
                Type = "https://binesh.errors/bad_request",
                Instance = httpContext.Request.Path,
                Extensions = { ["traceId"] = traceId, ["code"] = "bad_request" },
            };
        }

        return new ProblemDetails
        {
            Title = "Internal Server Error",
            Status = StatusCodes.Status500InternalServerError,
            Detail = "An unexpected error occurred.",
            Type = "https://binesh.errors/internal",
            Instance = httpContext.Request.Path,
            Extensions = { ["traceId"] = traceId, ["code"] = "internal" },
        };
    }
}
