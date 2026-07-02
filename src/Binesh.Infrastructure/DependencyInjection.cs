using Binesh.Application.Abstractions;
using Binesh.Infrastructure.Ai;
using Binesh.Infrastructure.Bi;
using Binesh.Infrastructure.Configuration;
using Binesh.Infrastructure.Persistence;
using Binesh.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;

namespace Binesh.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DatabaseSettings>()
            .Bind(configuration.GetSection(DatabaseSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<BiSourceSettings>()
            .Bind(configuration.GetSection(BiSourceSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddDbContext<BineshDbContext>((sp, options) =>
        {
            var settings = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            options.UseNpgsql(settings.ConnectionString, npg =>
            {
                npg.MigrationsAssembly(typeof(BineshDbContext).Assembly.GetName().Name);
                npg.EnableRetryOnFailure(settings.MaxRetryCount);
                npg.CommandTimeout(settings.CommandTimeoutSeconds);
            });
        });

        // Expose the same scoped instance via the abstraction so handlers can
        // inject IBineshDbContext.
        services.AddScoped<IBineshDbContext>(sp => sp.GetRequiredService<BineshDbContext>());
        services.AddDataProtection();
        services.AddScoped<IAiSettingsProtector, DataProtectionAiSettingsProtector>();
        services.AddScoped<IUserAiSettingsResolver, UserAiSettingsResolver>();
        services.AddSingleton<IBiAnalyticsService, BiAnalyticsService>();

        // ── Round 15 — object storage (MinIO) ────────────────────────────────
        services.AddOptions<MinioSettings>()
            .Bind(configuration.GetSection(MinioSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IMinioClient>(sp =>
        {
            var s = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            var builder = new MinioClient()
                .WithEndpoint(s.Endpoint)
                .WithCredentials(s.AccessKey, s.SecretKey);
            if (s.UseSsl) { builder = builder.WithSSL(); }
            return builder.Build();
        });

        services.AddSingleton<IFileStorage, MinioFileStorage>();
        services.AddHostedService<MinioBootstrapService>();

        return services;
    }
}
