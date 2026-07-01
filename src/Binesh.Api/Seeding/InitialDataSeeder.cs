using Binesh.Application.Abstractions;
using Binesh.Application.Features.Customers.CreateCustomer;
using Binesh.Application.Features.Products.CreateProduct;
using Binesh.Application.Features.Sales.CreateSale;
using Binesh.Application.Features.SalesReturns.CreateSalesReturn;
using Binesh.Domain.Customers;
using Binesh.Domain.Products;
using Binesh.Identity.Configuration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Binesh.Api.Seeding;

/// <summary>
/// Dev/demo data seeder. When <c>Seed:InitialData</c> is <c>true</c>, populates a
/// realistic dataset by sending the same MediatR CRUD commands the API endpoints
/// use — so it doubles as an end-to-end exercise of the command handlers, domain
/// factories, and validators. Idempotent: skips entirely if any sale exists.
///
/// Runs after <c>IdentityBootstrapService</c> (registration order) and, in the
/// docker stack, after the migrator has applied the schema.
/// </summary>
internal sealed class InitialDataSeeder(
    IServiceProvider services,
    IOptions<SeedSettings> seedOptions,
    ILogger<InitialDataSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!seedOptions.Value.InitialData)
        {
            return;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IBineshDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        if (await db.Sales.AnyAsync(cancellationToken))
        {
            logger.LogInformation("InitialData seeding skipped — sales already present.");
            return;
        }

        try
        {
            await SeedAsync(mediator, cancellationToken);
        }
        catch (Exception ex)
        {
            // Dev convenience only — never take the app down over demo data.
            logger.LogError(ex, "InitialData seeding failed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedAsync(IMediator mediator, CancellationToken ct)
    {
        // ── Products (across DetailedType categories + product types) ─────────
        var productSpecs = new (ProductType Type, string Code, string Desc, string Detailed)[]
        {
            (ProductType.Carpet, "CARP-600", "فرش ۶۰۰ شانه", "600 شانه"),
            (ProductType.Carpet, "CARP-700", "فرش ۷۰۰ شانه", "700 شانه"),
            (ProductType.Carpet, "CARP-1000", "فرش ۱۰۰۰ شانه", "1000 شانه"),
            (ProductType.Rug, "RUG-DAST", "قالیچه دستباف", "دستباف"),
            (ProductType.Rug, "RUG-MOKT", "موکت", "موکت"),
            (ProductType.RawMaterials, "RAW-NAKH", "نخ خام", "نخ"),
        };
        var productIds = new List<Guid>();
        foreach (var p in productSpecs)
        {
            var dto = await mediator.Send(new CreateProductCommand(p.Type, p.Code, p.Desc, p.Detailed), ct);
            productIds.Add(dto.Id);
        }

        // ── Customers (across types + regions) ────────────────────────────────
        var customerSpecs = new (CustomerType Type, string Name, string Family, string Province, string City)[]
        {
            (CustomerType.MoshtarianKhanegi, "علی", "احمدی", "تهران", "تهران"),
            (CustomerType.Bedehkaran, "سارا", "کریمی", "اصفهان", "اصفهان"),
            (CustomerType.Sherka, "شرکت پارس", "تجارت", "فارس", "شیراز"),
            (CustomerType.Bazaryab, "رضا", "محمدی", "خراسان رضوی", "مشهد"),
            (CustomerType.MoshtarianKhanegi, "مریم", "حسینی", "البرز", "کرج"),
            (CustomerType.Bestankar, "حسن", "رضایی", "آذربایجان شرقی", "تبریز"),
        };
        var customerIds = new List<Guid>();
        foreach (var c in customerSpecs)
        {
            var person = new PersonInput(
                c.Name, c.Family, Code: null, Phone: null, Mobile: null, Fax: null,
                Pelak: null, Address: null, BirthDate: null,
                Region: new RegionInput("ایران", c.Province, c.City));
            var dto = await mediator.Send(
                new CreateCustomerCommand(c.Type, Active: true, PaymentReliability: 0.75f, person), ct);
            customerIds.Add(dto.Id);
        }

        // ── Sales — deterministic spread over Jan–Apr 2026 ────────────────────
        var rng = new Random(42);
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var salesCount = 40;
        for (var i = 0; i < salesCount; i++)
        {
            var productId = productIds[rng.Next(productIds.Count)];
            var customerId = customerIds[rng.Next(customerIds.Count)];
            var date = start.AddDays(rng.Next(0, 120)).AddHours(rng.Next(0, 24));
            var quantity = rng.Next(1, 15);
            var unitPrice = rng.Next(2, 20) * 500_000L; // 1M – 10M rial-ish
            var delivered = rng.NextDouble() < 0.85 ? quantity : Math.Max(1, quantity - rng.Next(1, quantity + 1));

            await mediator.Send(new CreateSaleCommand(
                date, unitPrice * quantity, quantity, delivered, 1000 + i, productId, customerId), ct);
        }

        // ── A handful of returns so the summary's return card is non-zero ─────
        for (var i = 0; i < 6; i++)
        {
            var productId = productIds[rng.Next(productIds.Count)];
            var customerId = customerIds[rng.Next(customerIds.Count)];
            var date = start.AddDays(rng.Next(0, 120));
            var quantity = rng.Next(1, 4);
            var unitPrice = rng.Next(2, 20) * 500_000L;

            await mediator.Send(new CreateSalesReturnCommand(
                date, unitPrice * quantity, quantity, quantity, 5000 + i, productId, customerId), ct);
        }

        logger.LogWarning(
            "InitialData seeded: {Products} products, {Customers} customers, {Sales} sales, 6 returns.",
            productIds.Count, customerIds.Count, salesCount);
    }
}
