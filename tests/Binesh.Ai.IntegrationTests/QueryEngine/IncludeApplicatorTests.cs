using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;

namespace Binesh.Ai.IntegrationTests.QueryEngine;

public sealed class IncludeApplicatorTests
{
    private readonly EntitySchema _schema = SaleSchema.Build();

    [Fact]
    public void CollectPaths_NoNavFieldsReferenced_ReturnsEmpty()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", ["Price", "Date"], null),
            null, null, null, null);

        var paths = IncludeApplicator.CollectPaths(_schema, req);
        Assert.Empty(paths);
    }

    [Fact]
    public void CollectPaths_DropsShorterPathsCoveredByLonger()
    {
        // CustomerCity → "Counterparty.Person.Region" covers
        // "Counterparty.Person" and "Counterparty"; CustomerName uses
        // "Counterparty.Person"; CustomerPaymentReliability uses "Counterparty".
        var req = new AiQueryRequest("Sale",
            new AiSelect("list",
                ["CustomerCity", "CustomerName", "CustomerPaymentReliability"], null),
            null, null, null, null);

        var paths = IncludeApplicator.CollectPaths(_schema, req);
        Assert.Single(paths);
        Assert.Equal("Counterparty.Person.Region", paths[0], StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CollectPaths_UnionAcrossFiltersGroupByOrderBy()
    {
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", ["Price"], null),
            Filters: [new AiFilter("ProductCode", "eq", "X-1")],         // Product
            GroupBy: [new AiGroupBy("CustomerCity")],                    // Counterparty.Person.Region
            OrderBy: [new AiOrderBy("CustomerName", "asc")],             // Counterparty.Person
            Paging: null);

        var paths = IncludeApplicator.CollectPaths(_schema, req);
        Assert.Equal(2, paths.Count);
        Assert.Contains("Product", paths);
        Assert.Contains("Counterparty.Person.Region", paths);
    }

    [Fact]
    public void CollectPaths_UnknownFieldsAreSilentlyIgnored()
    {
        // The validator catches unknown fields; IncludeApplicator should never
        // throw — it just contributes nothing for them.
        var req = new AiQueryRequest("Sale",
            new AiSelect("list", ["Price", "NotARealField"], null),
            null, null, null, null);

        var paths = IncludeApplicator.CollectPaths(_schema, req);
        Assert.Empty(paths);
    }
}
