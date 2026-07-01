using Binesh.Ai.Orchestration;
using Binesh.Api.IntegrationTests.Fakes;
using Binesh.Application.Abstractions;
using Binesh.Identity.Services;
using Binesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Binesh.Api.IntegrationTests;

/// <summary>
/// Boots the real Binesh.Api host wired to a per-fixture Postgres container.
/// Each xUnit collection that uses this fixture gets its own database lifetime;
/// tests inside a fixture share the same DB and should clean up after themselves.
///
/// Reference template — every integration test class follows this shape:
/// implement <see cref="IClassFixture{TFixture}"/> over BineshApiFactory.
/// </summary>
public class BineshApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("binesh_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    /// <summary>The fake SMS sender registered in test mode — read OTPs from here.</summary>
    public CapturingSmsSender Sms { get; } = new();

    /// <summary>
    /// The scripted AI chat client registered in test mode. Tests that exercise
    /// <c>POST /api/ai/query</c> push scripted responses into this; everyone
    /// else can ignore it.
    /// </summary>
    public ScriptedAiChatClient AiChat { get; } = new();

    /// <summary>
    /// Test-mode <see cref="IFileStorage"/>. Records every call and lets
    /// profile-image tests simulate completed uploads without MinIO.
    /// </summary>
    public InMemoryFileStorage FileStorage { get; } = new();

    public const string SuperAdminPhone = "+989999999999";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // Apply migrations using a standalone DbContext BEFORE we touch Services.
        // Touching Services starts the host, which triggers IdentityBootstrapService
        // — and the bootstrap queries AspNetRoles, which fails if the table doesn't
        // exist yet.
        var options = new DbContextOptionsBuilder<BineshDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        await using (var db = new BineshDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Force host build now that the schema is in place. SuperAdmin seed runs here.
        _ = Services;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("Database:ConnectionString", _postgres.GetConnectionString());

        // Required by ValidateOnStart even though tests never call OpenAI.
        builder.UseSetting("OpenAI:ApiKey", "sk-test-placeholder");
        builder.UseSetting("OpenAI:BaseUrl", "https://api.openai.com/v1");
        builder.UseSetting("OpenAI:Model", "gpt-4o-mini");

        // Required by Minio ValidateOnStart even though tests use the fake.
        builder.UseSetting("Minio:Endpoint", "fake-minio.test:9000");
        builder.UseSetting("Minio:AccessKey", "test-access");
        builder.UseSetting("Minio:SecretKey", "test-secret");
        builder.UseSetting("Minio:BucketName", "binesh-test");

        // Don't make tests wait for OTP resend windows.
        builder.UseSetting("Identity:Otp:ResendDelay", "00:00:00");

        // Bootstrap a SuperAdmin so tests can sign in as one.
        builder.UseSetting("Seed:SuperAdmin:PhoneNumber", SuperAdminPhone);
        builder.UseSetting("Seed:SuperAdmin:FirstName", "Test");
        builder.UseSetting("Seed:SuperAdmin:LastName", "SuperAdmin");

        // Disable rate limits for tests — all tests share the same loopback IP
        // so the prod 5/min cap on auth bites within a single test run.
        builder.UseSetting("RateLimit:Auth:PermitLimit", "10000");
        builder.UseSetting("RateLimit:Ai:PermitLimit", "10000");
        builder.UseSetting("RateLimit:Default:PermitLimit", "10000");

        builder.ConfigureServices(services =>
        {
            // Replace SMS sender with the capturing fake.
            var existing = services.Single(d => d.ServiceType == typeof(ISmsSender));
            services.Remove(existing);
            services.AddSingleton<ISmsSender>(Sms);

            // Replace the AI chat client with the scripted fake so tests never
            // reach real OpenAI.
            var chatRegs = services.Where(d => d.ServiceType == typeof(IAiChatClient)).ToList();
            foreach (var r in chatRegs) { services.Remove(r); }
            services.AddSingleton<IAiChatClient>(AiChat);

            // Replace IFileStorage so we never reach real MinIO. Also remove
            // the bootstrap hosted service that pings the real bucket.
            var storageRegs = services.Where(d => d.ServiceType == typeof(IFileStorage)).ToList();
            foreach (var r in storageRegs) { services.Remove(r); }
            services.AddSingleton<IFileStorage>(FileStorage);

            var bootstrapRegs = services.Where(d =>
                d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                && d.ImplementationType?.FullName?.Contains("MinioBootstrapService") == true).ToList();
            foreach (var r in bootstrapRegs) { services.Remove(r); }
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
