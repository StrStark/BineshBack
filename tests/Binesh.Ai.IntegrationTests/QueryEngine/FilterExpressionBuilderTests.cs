using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Domain.Products;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

/// <summary>
/// LINQ-to-objects unit tests for the filter expression tree. Doesn't need
/// EF to exercise the predicate — just composes against an in-memory list.
/// </summary>
public sealed class FilterExpressionBuilderTests
{
    private static readonly Guid CompanyId = Guid.NewGuid();
    private readonly EntitySchema _schema = ProductSchema.Build();

    private readonly List<Product> _products =
    [
        Product.Create(CompanyId, ProductType.Carpet, "P-1", "C1", "600"),
        Product.Create(CompanyId, ProductType.Carpet, "P-2", "C2", "700"),
        Product.Create(CompanyId, ProductType.Rug,    "R-1", "R1", "small"),
    ];

    [Fact]
    public void Filter_StringEquality()
    {
        var q = _products.AsQueryable();
        var filtered = FilterExpressionBuilder.Apply(q, _schema,
            [new AiFilter("ProductCode", "eq", "R-1")]);
        Assert.Single(filtered.ToList());
        Assert.Equal("R-1", filtered.First().ProductCode);
    }

    [Fact]
    public void Filter_EnumEquality_ParsesString()
    {
        var q = _products.AsQueryable();
        var filtered = FilterExpressionBuilder.Apply(q, _schema,
            [new AiFilter("Type", "eq", "Carpet")]);
        Assert.Equal(2, filtered.Count());
    }

    [Fact]
    public void Filter_MultipleClausesAreAnded()
    {
        var q = _products.AsQueryable();
        var filtered = FilterExpressionBuilder.Apply(q, _schema,
        [
            new AiFilter("Type", "eq", "Carpet"),
            new AiFilter("ProductCode", "ne", "P-1"),
        ]);
        Assert.Single(filtered.ToList());
        Assert.Equal("P-2", filtered.First().ProductCode);
    }
}
