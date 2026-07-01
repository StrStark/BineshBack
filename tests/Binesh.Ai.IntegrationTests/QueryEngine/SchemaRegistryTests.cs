using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

public sealed class SchemaRegistryTests
{
    [Fact]
    public void Register_DuplicateName_Throws()
    {
        var r = new SchemaRegistry();
        r.Register(CustomerSchema.Build());
        Assert.Throws<InvalidOperationException>(() => r.Register(CustomerSchema.Build()));
    }

    [Fact]
    public void Get_UnknownSchema_Throws()
    {
        var r = new SchemaRegistry();
        Assert.Throws<InvalidOperationException>(() => r.Get("Nope"));
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var r = new SchemaRegistry();
        r.Register(CustomerSchema.Build());

        Assert.Same(r.Get("Customer"), r.Get("customer"));
        Assert.Same(r.Get("Customer"), r.Get("CUSTOMER"));
    }

    [Fact]
    public void TryGet_MissingReturnsFalse()
    {
        var r = new SchemaRegistry();
        var ok = r.TryGet("Nope", out var schema);
        Assert.False(ok);
        Assert.Null(schema);
    }

    [Fact]
    public void All_BuiltInSchemas_Register()
    {
        var r = new SchemaRegistry();
        r.Register(CustomerSchema.Build());
        r.Register(ProductSchema.Build());
        r.Register(InventoryEventSchema.Build());
        r.Register(FinancialSchema.Build());
        r.Register(SaleSchema.Build());
        r.Register(SalesReturnSchema.Build());

        Assert.Equal(6, r.All.Count);
    }
}
