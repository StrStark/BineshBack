using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Binesh.Application.Behaviors;

/// <summary>
/// Logs every MediatR request with its duration. One place — instead of the
/// 56 try/catch + LogError calls scattered across the old controllers.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogDebug("Handling {Request}", name);

        try
        {
            var response = await next();
            logger.LogInformation("Handled {Request} in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Handler {Request} failed after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
