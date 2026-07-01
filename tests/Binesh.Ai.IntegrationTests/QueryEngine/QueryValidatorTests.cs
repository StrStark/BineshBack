using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

public sealed class QueryValidatorTests
{
    private readonly EntitySchema _schema = SaleSchema.Build();

    [Fact]
    public void ValidList_NoThrow()
    {
        var req = new AiQueryRequest(
            Entity: "Sale",
            Select: new AiSelect("list", Fields: ["Price", "Date"], Aggregates: null),
            Filters: null, GroupBy: null, OrderBy: null, Paging: null);
        QueryValidator.Validate(_schema, req);   // does not throw
    }

    [Fact]
    public void UnknownField_InSelect_Throws()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", ["NotAField"], null), null, null, null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
        Assert.Contains("NotAField", ex.Message);
    }

    [Fact]
    public void UnknownOperator_OnFilter_Throws()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", null, null),
            Filters: [new AiFilter("Price", "contains", 100)],
            null, null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
        Assert.Contains("contains", ex.Message);
        Assert.Contains("Price", ex.Message);
    }

    [Fact]
    public void NonAggregatableField_InAggregate_Throws()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("aggregate", null, [new AiAggregate("sum", "DocNumber", "totalDocs")]),
            null, null, null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
        Assert.Contains("DocNumber", ex.Message);
    }

    [Fact]
    public void GroupByWithoutAggregate_Throws()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("aggregate", null, null),  // aggregates missing
            null,
            GroupBy: [new AiGroupBy("Date")],
            null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
        Assert.Contains("Aggregates", ex.Message);
    }

    [Fact]
    public void OrderBy_NonOrderableField_Throws()
    {
        var schema = ProductSchema.Build();
        var req = new AiQueryRequest("Product",
            new AiSelect("list", null, null),
            null, null,
            OrderBy: [new AiOrderBy("Type", "asc")],  // Type is Orderable=false on ProductSchema
            null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(schema, req));
        Assert.Contains("orderable", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Paging_NegativeSkip_Throws()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", null, null), null, null, null,
            Paging: new AiPaging(-1, 10));
        Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
    }

    [Fact]
    public void EnumFilter_InvalidValue_Throws()
    {
        var schema = ProductSchema.Build();
        var req = new AiQueryRequest("Product",
            new AiSelect("list", null, null),
            Filters: [new AiFilter("Type", "eq", "NotACarpet")],
            null, null, null);
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(schema, req));
        Assert.Contains("NotACarpet", ex.Message);
    }

    [Fact]
    public void MultipleErrors_AreReportedTogether()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", ["NotAField"], null),
            Filters: [new AiFilter("Price", "contains", 1)],
            null, null,
            Paging: new AiPaging(-1, 10));
        var ex = Assert.Throws<InvalidOperationException>(() => QueryValidator.Validate(_schema, req));
        // All three errors should appear in the joined message.
        Assert.Contains("NotAField", ex.Message);
        Assert.Contains("contains", ex.Message);
        Assert.Contains("Skip", ex.Message);
    }
}
