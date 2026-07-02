using Binesh.Ai.QueryEngine;
using Binesh.Domain.Customers;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;
using Binesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Binesh.Api.IntegrationTests.Features.AiQuery;

/// <summary>
/// Round 12b — exercise <see cref="AiQueryEngine"/> against the real Postgres
/// container used by the rest of the integration suite. These tests verify
/// that the SQL-translatable rewrite of <c>AggregateExecutor</c> actually
/// translates to SQL <c>GROUP BY</c> — they cannot run on an in-memory list
/// queryable because the legacy code's bug only manifests at the DB layer.
/// </summary>
public sealed class AiQueryEngineSqlTests(BineshApiFactory factory)
    : IClassFixture<BineshApiFactory>, IAsyncLifetime
{
    private readonly AiQueryEngine _engine = factory.Services.GetRequiredService<AiQueryEngine>();
    private readonly SchemaRegistry _registry = factory.Services.GetRequiredService<SchemaRegistry>();

    private Guid _productCarpet;
    private Guid _productRug;
    private Guid _customerTehran;
    private Guid _companyId;

    public async Task InitializeAsync()
    {
        await ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── List mode ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ListMode_ReturnsProjectedRows()
    {
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var request = new AiQueryRequest(
            Entity: "Sale",
            Select: new AiSelect("list", Fields: ["Price", "ProductCode"], Aggregates: null),
            Filters: null, GroupBy: null,
            OrderBy: [new AiOrderBy("Price", "desc")],
            Paging: new AiPaging(0, 10));

        var result = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)
            await _engine.ExecuteAsync(schema, db.Sales, request, default);

        Assert.NotEmpty(result);
        Assert.All(result, row =>
        {
            Assert.Contains("Price", row.Keys);
            Assert.Contains("ProductCode", row.Keys);
        });
        // Descending order is enforced.
        var prices = result.Select(r => (long)r["Price"]!).ToList();
        Assert.Equal(prices.OrderByDescending(x => x).ToList(), prices);
    }

    [Fact]
    public async Task ListMode_RespectsFilter()
    {
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var request = new AiQueryRequest("Sale",
            new AiSelect("list", ["Price"], null),
            Filters: [new AiFilter("ProductCode", "eq", "AI-CARPET")],
            null, null, null);

        var result = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)
            await _engine.ExecuteAsync(schema, db.Sales, request, default);

        // Seed has 3 carpet sales out of 4 total.
        Assert.Equal(3, result.Count);
    }

    // ── Flat aggregate ─────────────────────────────────────────────────────

    [Fact]
    public async Task FlatAggregate_SumAndCount()
    {
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var request = new AiQueryRequest("Sale",
            new AiSelect("aggregate", null,
            [
                new AiAggregate("sum", "Price", "totalPrice"),
                new AiAggregate("count", "Price", "rowCount"),
            ]),
            null, null, null, null);

        var result = (IReadOnlyDictionary<string, object?>)
            await _engine.ExecuteAsync(schema, db.Sales, request, default);

        // 3000 + 5000 + 2000 + 9000 = 19000
        Assert.Equal(19000L, Convert.ToInt64(result["totalPrice"]));
        Assert.Equal(4L, Convert.ToInt64(result["rowCount"]));
    }

    // ── Grouped aggregate ─────────────────────────────────────────────────

    [Fact]
    public async Task GroupedAggregate_ByProductCode()
    {
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var request = new AiQueryRequest("Sale",
            new AiSelect("aggregate", null,
            [
                new AiAggregate("sum", "Price", "totalPrice"),
                new AiAggregate("count", "Price", "rowCount"),
            ]),
            null,
            GroupBy: [new AiGroupBy("ProductCode")],
            null, null);

        var rows = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)
            await _engine.ExecuteAsync(schema, db.Sales, request, default);

        Assert.Equal(2, rows.Count);  // AI-CARPET, AI-RUG
        var byCode = rows.ToDictionary(r => (string)r["ProductCode"]!);

        // AI-CARPET: 3000 + 5000 + 2000 = 10000 (3 rows)
        Assert.Equal(10000L, Convert.ToInt64(byCode["AI-CARPET"]["totalPrice"]));
        Assert.Equal(3, Convert.ToInt32(byCode["AI-CARPET"]["rowCount"]));

        // AI-RUG: 9000 (1 row)
        Assert.Equal(9000L, Convert.ToInt64(byCode["AI-RUG"]["totalPrice"]));
        Assert.Equal(1, Convert.ToInt32(byCode["AI-RUG"]["rowCount"]));
    }

    [Fact]
    public async Task GroupedAggregate_BehavesIdenticallyForLargeRowCount()
    {
        // Smoke check the SQL-pushed GroupBy actually scales — the legacy bug
        // would surface here by materializing all 5_000 rows into memory before
        // grouping. With the new code, only 2 group rows ever cross the wire.
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        // Seed 5_000 extra rows split between the two products.
        var bulk = new List<Sale>(5_000);
        for (var i = 0; i < 5_000; i++)
        {
            bulk.Add(Sale.Create(
                _companyId,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(i),
                price: i,
                quantity: 1, deliveredQuantity: 1, docNumber: 9000 + i,
                productId: i % 2 == 0 ? _productCarpet : _productRug,
                counterpartyId: _customerTehran));
        }
        db.Sales.AddRange(bulk);
        await db.SaveChangesAsync();

        var request = new AiQueryRequest("Sale",
            new AiSelect("aggregate", null, [new AiAggregate("count", "Price", "rowCount")]),
            null,
            GroupBy: [new AiGroupBy("ProductCode")],
            null, null);

        var rows = (IReadOnlyList<IReadOnlyDictionary<string, object?>>)
            await _engine.ExecuteAsync(schema, db.Sales, request, default);

        Assert.Equal(2, rows.Count);
        // Each product has 2_500 + (seed rows from InitializeAsync). Just
        // verify the two groups roll up the expected combined totals.
        var sum = rows.Sum(r => Convert.ToInt32(r["rowCount"]));
        Assert.Equal(4 + 5_000, sum);   // 4 seed sales + 5_000 bulk
    }

    // ── Validation surfaces from inside the engine ──────────────────────────

    [Fact]
    public async Task UnknownField_Throws()
    {
        var schema = _registry.Get("Sale");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();

        var bad = new AiQueryRequest("Sale",
            new AiSelect("list", ["BogusField"], null), null, null, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _engine.ExecuteAsync(schema, db.Sales, bad, default));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task ResetAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        await db.Sales.ExecuteDeleteAsync();
        await db.Customers.ExecuteDeleteAsync();
        await db.Persons.ExecuteDeleteAsync();
        await db.Products.ExecuteDeleteAsync();
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BineshDbContext>();
        _companyId = await db.Companies.Select(c => c.Id).FirstAsync();

        var carpet = Product.Create(_companyId, ProductType.Carpet, "AI-CARPET", "Engine fixture carpet", "600");
        var rug = Product.Create(_companyId, ProductType.Rug, "AI-RUG", "Engine fixture rug", "small");
        var person = Person.Create("Engine", "Buyer", null, null, "0921", null, null, null, null, null);
        var customer = Customer.Create(_companyId, CustomerType.MoshtarianKhanegi, true, 0.8f, person);
        db.Products.AddRange(carpet, rug);
        db.Customers.Add(customer);
        await db.SaveChangesAsync();

        _productCarpet = carpet.Id;
        _productRug = rug.Id;
        _customerTehran = customer.Id;

        Sale NewSale(long price, Guid productId) => Sale.Create(
            _companyId,
            new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc),
            price, quantity: 1, deliveredQuantity: 1, docNumber: (int)price,
            productId: productId, counterpartyId: customer.Id);

        db.Sales.AddRange(
            NewSale(3000, carpet.Id),
            NewSale(5000, carpet.Id),
            NewSale(2000, carpet.Id),
            NewSale(9000, rug.Id));
        await db.SaveChangesAsync();
    }
}
