using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Binesh.Application.Abstractions;
using Binesh.Ai.Configuration;
using Binesh.Api.Configuration;
using Binesh.Api.ErrorHandling;
using Binesh.Api.HealthChecks;
using Binesh.Api.Tenancy;
using Binesh.Infrastructure.Configuration;
using HealthChecks.NpgSql;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

namespace Binesh.Api.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything that's API-layer specific: CORS, rate limiting,
    /// exception handler, health checks, Swagger. Settings & layer modules
    /// (Application/Infrastructure/Identity/Ai) are wired in Program.cs.
    /// </summary>
    public static IServiceCollection AddBineshApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CorsSettings>()
            .Bind(configuration.GetSection(CorsSettings.SectionName))
            .ValidateOnStart();

        // JSON: serialize enums as their string name (self-documenting payloads)
        // but ALSO accept integer values on the way in so legacy / generated
        // clients keep working.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(
                new JsonStringEnumConverter(allowIntegerValues: true));
        });

        services.AddOptions<RateLimitSettings>()
            .Bind(configuration.GetSection(RateLimitSettings.SectionName))
            .ValidateOnStart();

        services.AddCors();   // policy added in UsePipeline so we can read bound settings

        // Rate limiting — named policies, applied per-endpoint by the slices that need them.
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", httpContext =>
            {
                var settings = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitSettings>>().Value.Auth;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy("ai", httpContext =>
            {
                var settings = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitSettings>>().Value.Ai;

                var partitionKey = httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anon";

                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                    });
            });

            options.AddPolicy("default", httpContext =>
            {
                var settings = httpContext.RequestServices
                    .GetRequiredService<IOptions<RateLimitSettings>>().Value.Default;

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.User.Identity?.Name
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "anon",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = settings.PermitLimit,
                        Window = TimeSpan.FromSeconds(settings.WindowSeconds),
                        QueueLimit = 0,
                    });
            });
        });

        // RFC 7807 problem details everywhere
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        // Health
        var healthBuilder = services.AddHealthChecks()
            .AddCheck<OpenAiHealthCheck>("openai", tags: ["ready"]);

        var connString = configuration.GetSection(DatabaseSettings.SectionName)["ConnectionString"];
        if (!string.IsNullOrWhiteSpace(connString))
        {
            healthBuilder.AddNpgSql(connString, name: "postgres", tags: ["ready"]);
        }

        // Minimal API explorer + Swagger (dev only — pipeline guards the endpoint)
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opts =>
        {
            opts.SwaggerDoc("v1", new OpenApiInfo { Title = "Binesh API", Version = "v1" });

            opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT bearer token. Paste the access token only — no 'Bearer ' prefix.",
            });

            opts.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });

        return services;
    }
}
