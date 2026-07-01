using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

public sealed class EntitySchemaTests
{
    [Fact]
    public void GetField_IsCaseInsensitive()
    {
        var schema = CustomerSchema.Build();
        Assert.Same(schema.GetField("Mobile"), schema.GetField("mobile"));
        Assert.Same(schema.GetField("Mobile"), schema.GetField("MOBILE"));
    }

    [Fact]
    public void GetField_UnknownThrows()
    {
        var schema = CustomerSchema.Build();
        var ex = Assert.Throws<InvalidOperationException>(() => schema.GetField("NotAField"));
        Assert.Contains("NotAField", ex.Message);
        Assert.Contains("Customer", ex.Message);
    }

    [Fact]
    public void TryGetField_MissingReturnsFalse()
    {
        var schema = CustomerSchema.Build();
        var ok = schema.TryGetField("NotAField", out var d);
        Assert.False(ok);
        Assert.Null(d);
    }
}
