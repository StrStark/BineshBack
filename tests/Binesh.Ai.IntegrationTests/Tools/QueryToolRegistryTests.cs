using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;

namespace Binesh.Ai.IntegrationTests.Tools;

public sealed class QueryToolRegistryTests
{
    [Fact]
    public void Register_DuplicateName_Throws()
    {
        var registry = new QueryToolRegistry();
        registry.Register(new StubTool("query_x", CustomerSchema.Build()));
        Assert.Throws<InvalidOperationException>(
            () => registry.Register(new StubTool("query_x", ProductSchema.Build())));
    }

    [Fact]
    public void Get_LookupIsCaseInsensitive()
    {
        var registry = new QueryToolRegistry();
        registry.Register(new StubTool("query_sale", SaleSchema.Build()));
        Assert.NotNull(registry.Get("QUERY_SALE"));
        Assert.NotNull(registry.Get("query_sale"));
        Assert.Null(registry.Get("query_bogus"));
    }

    private sealed class StubTool(string toolName, EntitySchema schema) : IQueryableTool
    {
        public string ToolName { get; } = toolName;
        public string Description => "stub";
        public EntitySchema Schema { get; } = schema;
        public Task<object> ExecuteAsync(AiQueryRequest r, CancellationToken c)
            => Task.FromResult<object>(new { });
    }
}
