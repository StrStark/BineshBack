using Binesh.Api.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

namespace Binesh.Api.Extensions;

public static class WebApplicationExtensions
{
    /// <summary>
    /// Wires the request pipeline. Order matters — keep it intentional.
    /// </summary>
    public static WebApplication UseBineshPipeline(this WebApplication app)
    {
        // Errors first so anything thrown downstream becomes ProblemDetails.
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Structured request logging (one line per request, with timings).
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // CORS policy from bound settings — single named "default" policy,
        // exact origins only. Replaces the dual-policy mess of the old code.
        var cors = app.Services.GetRequiredService<IOptions<CorsSettings>>().Value;
        app.UseCors(policy =>
        {
            if (cors.AllowedOrigins.Length > 0)
            {
                policy.WithOrigins(cors.AllowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            }
        });

        app.UseRateLimiter();

        // WebSockets — used by the AI chat streaming endpoint. Must sit before
        // routing/auth so the handler can pull a query-string ticket and
        // skip Bearer auth which WS clients can't easily send.
        app.UseWebSockets();

        app.UseAuthentication();
        app.UseAuthorization();

        // /healthz — liveness. Always 200 if the process is alive.
        app.MapHealthChecks("/healthz", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        // /readyz — readiness. 200 only if all ready-tagged checks pass.
        app.MapHealthChecks("/readyz", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        // Root status (handy for smoke tests and load-balancer probes).
        app.MapGet("/", (IHostEnvironment env) => Results.Ok(new
        {
            service = "Binesh.Api",
            status = "running",
            environment = env.EnvironmentName,
            time = DateTime.UtcNow,
        }));

        return app;
    }
}
