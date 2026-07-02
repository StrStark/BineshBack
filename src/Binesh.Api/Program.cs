using Binesh.Ai;
using Binesh.Api.Endpoints.Analytics;
using Binesh.Api.Endpoints.Ai;
using Binesh.Api.Endpoints.Auth;
using Binesh.Api.Endpoints.Chat;
using Binesh.Api.Endpoints.Companies;
using Binesh.Api.Endpoints.Customers;
using Binesh.Api.Endpoints.Dashboards;
using Binesh.Api.Endpoints.Financial;
using Binesh.Api.Endpoints.Products;
using Binesh.Api.Endpoints.Sales;
using Binesh.Api.Endpoints.SalesReturns;
using Binesh.Api.Endpoints.Tickets;
using Binesh.Api.Endpoints.Users;
using Binesh.Api.Endpoints.Warehouse;
using Binesh.Api.Extensions;
using Binesh.Application;
using Binesh.Identity;
using Binesh.Infrastructure;
using Binesh.Infrastructure.Persistence;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Serilog;

// Load .env if present (dev convenience). In prod, env vars come from the
// container orchestrator and this is a no-op when the file is absent.
Env.TraversePath().Load();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Env vars override JSON; secrets MUST come from here in prod.
    builder.Configuration.AddEnvironmentVariables();

    // Wire Serilog from configuration. Earlier we used a bootstrap logger pattern
    // (Log.Logger = CreateBootstrapLogger()) but it doesn't survive across the
    // multiple host builds that WebApplicationFactory does during integration
    // tests ("logger is already frozen"). The trade-off: errors thrown *before*
    // Host.UseSerilog runs (i.e. inside CreateBuilder itself) get the default
    // console logger, which in practice is fine.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());

    // Layer wiring — each module owns its own DI extension.
    // AddApplication picks up MediatR handlers + validators from both Application
    // and Identity (since Identity contributes the auth slices).
    builder.Services.AddApplication(
        Binesh.Identity.DependencyInjection.Assembly,
        Binesh.Ai.DependencyInjection.Assembly);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddBineshIdentity(builder.Configuration);
    builder.Services.AddBineshAi(builder.Configuration);

    // API plumbing (CORS, rate limiting, exception handler, health, Swagger).
    builder.Services.AddBineshApi(builder.Configuration);

    // Dev/demo data seeder (gated by Seed:InitialData). Registered after
    // AddBineshIdentity so it runs after the SuperAdmin/roles bootstrap.
    builder.Services.AddHostedService<Binesh.Api.Seeding.InitialDataSeeder>();

    var app = builder.Build();

    // One-shot subcommand: `dotnet Binesh.Api.dll migrate`
    // Used by the migrator container in docker compose. Applies pending EF
    // migrations and exits. No HTTP listener is started.
    if (args.Length > 0 && string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        Log.Information("Applying pending migrations...");
        await db.Database.MigrateAsync();
        Log.Information("Migrations complete.");
        return;
    }

    app.UseBineshPipeline();

    // Feature endpoints — one Map<Area>Endpoints() call per feature area.
    app.MapAuthEndpoints();
    app.MapUsersEndpoints();
    app.MapCustomersEndpoints();
    app.MapProductsEndpoints();
    app.MapSalesEndpoints();
    app.MapSalesReturnsEndpoints();
    app.MapFinancialEndpoints();
    app.MapCompaniesEndpoints();
    app.MapDashboardEndpoints();
    app.MapTicketsEndpoints();
    app.MapAnalyticsEndpoints();
    app.MapWarehouseEndpoints();
    app.MapAiEndpoints();
    app.MapAiSettingsEndpoints();
    app.MapChatEndpoints();
    app.MapChatStreamingEndpoints();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // No bootstrap logger; print to stderr so docker compose / journald captures it.
    Console.Error.WriteLine($"Binesh.Api failed to start: {ex}");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration
/// tests can boot the real host.
/// </summary>
public partial class Program;
