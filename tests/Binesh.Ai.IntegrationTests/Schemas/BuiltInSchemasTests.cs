using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Domain.Customers;
using Binesh.Domain.Financial;
using Binesh.Domain.Products;
using Binesh.Domain.Sales;

namespace Binesh.Ai.IntegrationTests.Schemas;

/// <summary>
/// Round 12a — each built-in schema must round-trip a sample entity through
/// every declared field via the <see cref="CompiledSelectorCache"/>. This
/// catches typos in <c>Selector</c> expressions and field/type mismatches at
/// build time of the test, not at first runtime AI call.
/// </summary>
public sealed class BuiltInSchemasTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private readonly CompiledSelectorCache _cache = new();

    [Fact]
    public void CustomerSchema_AllFieldsResolve()
    {
        var schema = CustomerSchema.Build();
        var region = Region.Create("Iran", "Tehran", "Tehran");
        var person = Person.Create("Ali", "Ahmadi", "C-1", "021", "0912", "fax", "12", "Addr",
            new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero), region);
        var customer = Customer.Create(CompanyId, CustomerType.MoshtarianKhanegi, true, 0.8f, person);

        AssertEveryFieldResolves(schema, customer);
    }

    [Fact]
    public void ProductSchema_AllFieldsResolve()
    {
        var schema = ProductSchema.Build();
        var product = Product.Create(CompanyId, ProductType.Carpet, "P-1", "Carpet", "600 reed");
        AssertEveryFieldResolves(schema, product);
    }

    [Fact]
    public void InventoryEventSchema_AllFieldsResolve()
    {
        var schema = InventoryEventSchema.Build();
        var product = Product.Create(CompanyId, ProductType.Carpet, "P-1", "Carpet", "600 reed");
        var ev = InventoryEvent.Create(product.Id, InventoryEventType.Receipt,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            quantity: 10, unitPrice: 100, totalPrice: 1000, factorNumber: 1,
            value1: null, value2: null, value3: null, description: "first lot");
        AssertEveryFieldResolves(schema, ev);
    }

    [Fact]
    public void FinancialSchema_AllFieldsResolve()
    {
        var schema = FinancialSchema.Build();
        var entry = FinancialEntry.Create(CompanyId, "1001", "Cash", "Asset", debit: 500, credit: 0);
        AssertEveryFieldResolves(schema, entry);
    }

    [Fact]
    public void SaleSchema_AllFieldsResolve()
    {
        var schema = SaleSchema.Build();
        var sale = NewSale();
        AssertEveryFieldResolves(schema, sale);
    }

    [Fact]
    public void SalesReturnSchema_AllFieldsResolve()
    {
        var schema = SalesReturnSchema.Build();
        var (product, customer) = NewProductAndCustomer();
        var ret = SalesReturn.Create(
            CompanyId,
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            price: 200, quantity: 1, deliveredQuantity: 1, docNumber: 99,
            productId: product.Id, counterpartyId: customer.Id);
        SetNavigation(ret, "Product", product);
        SetNavigation(ret, "Counterparty", customer);
        AssertEveryFieldResolves(schema, ret);
    }

    private void AssertEveryFieldResolves(EntitySchema schema, object entity)
    {
        foreach (var field in schema.Fields)
        {
            var getter = _cache.Get(schema.EntityType, field);
            // Just verify it doesn't throw; the value may be null for nullable navigations.
            _ = getter(entity);
        }
    }

    private static Sale NewSale()
    {
        var (product, customer) = NewProductAndCustomer();
        var sale = Sale.Create(
            CompanyId,
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            price: 1000, quantity: 2, deliveredQuantity: 2, docNumber: 5,
            productId: product.Id, counterpartyId: customer.Id);
        SetNavigation(sale, "Product", product);
        SetNavigation(sale, "Counterparty", customer);
        return sale;
    }

    private static (Product, Customer) NewProductAndCustomer()
    {
        var product = Product.Create(CompanyId, ProductType.Carpet, "P-1", "Carpet", "600 reed");
        var region = Region.Create("Iran", "Tehran", "Tehran");
        var person = Person.Create("Ali", "Ahmadi", null, null, "0912", null, null, null, null, region);
        var customer = Customer.Create(CompanyId, CustomerType.MoshtarianKhanegi, true, 0.8f, person);
        return (product, customer);
    }

    /// <summary>
    /// Backing-field nav setter for the test only — Sale/SalesReturn entities
    /// have private setters and EF normally hydrates the navigation properties.
    /// </summary>
    private static void SetNavigation(object entity, string propName, object value)
    {
        var prop = entity.GetType().GetProperty(propName)
            ?? throw new InvalidOperationException($"No property {propName} on {entity.GetType().Name}");
        var setter = prop.GetSetMethod(nonPublic: true)
            ?? throw new InvalidOperationException($"Property {propName} has no setter");
        setter.Invoke(entity, new[] { value });
    }
}
