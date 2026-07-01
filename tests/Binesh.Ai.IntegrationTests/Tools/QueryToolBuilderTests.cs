using System.Text.Json;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;
using Binesh.Application.Abstractions;

namespace Binesh.Ai.IntegrationTests.Tools;

/// <summary>
/// The legacy <c>QueryToolBuilder</c> emitted ONE function definition whose
/// field-enum was the union of every schema's fields, letting the LLM
/// reference a Customer field on a Sale query. These tests enforce the fix:
/// every per-tool JSON schema must list ONLY that tool's own schema fields.
/// </summary>
public sealed class QueryToolBuilderTests
{
    private static IQueryableTool MakeTool(EntitySchema schema, string toolName)
        => new StubTool(schema, toolName);

    [Fact]
    public void PerTool_FieldEnum_ContainsOnlyThatSchemasFields()
    {
        var customerTool = MakeTool(CustomerSchema.Build(), "query_customer");
        var saleTool = MakeTool(SaleSchema.Build(), "query_sale");

        var customerFields = ExtractFieldEnum(QueryToolBuilder.BuildParametersJson(customerTool));
        var saleFields = ExtractFieldEnum(QueryToolBuilder.BuildParametersJson(saleTool));

        // No Sale-only field can appear in the Customer tool's enum, and vice-versa.
        Assert.DoesNotContain("DocNumber", customerFields);  // Sale field
        Assert.DoesNotContain("CustomerType", saleFields);    // Customer field
        Assert.Contains("Mobile", customerFields);
        Assert.Contains("Price", saleFields);
    }

    [Fact]
    public void EntityEnum_OnlyContainsThisToolsSchemaName()
    {
        var tool = MakeTool(ProductSchema.Build(), "query_product");
        var doc = JsonDocument.Parse(QueryToolBuilder.BuildParametersJson(tool));
        var entityEnum = doc.RootElement
            .GetProperty("properties").GetProperty("Entity").GetProperty("enum");
        Assert.Equal(1, entityEnum.GetArrayLength());
        Assert.Equal("Product", entityEnum[0].GetString());
    }

    [Fact]
    public void Operators_AreLimitedToTheSupportedSet()
    {
        var tool = MakeTool(SaleSchema.Build(), "query_sale");
        var doc = JsonDocument.Parse(QueryToolBuilder.BuildParametersJson(tool));
        var ops = doc.RootElement
            .GetProperty("properties").GetProperty("Filters")
            .GetProperty("items").GetProperty("properties")
            .GetProperty("Operator").GetProperty("enum");
        var values = Enumerable.Range(0, ops.GetArrayLength()).Select(i => ops[i].GetString()).ToHashSet();
        Assert.Equal(new HashSet<string?> { "eq", "ne", "ge", "le", "gt", "lt" }, values);
    }

    private static HashSet<string?> ExtractFieldEnum(string json)
    {
        // Walk to properties.Filters.items.properties.Field.enum, which is a list of field names.
        var doc = JsonDocument.Parse(json);
        var fieldEnum = doc.RootElement
            .GetProperty("properties").GetProperty("Filters")
            .GetProperty("items").GetProperty("properties")
            .GetProperty("Field").GetProperty("enum");
        return Enumerable.Range(0, fieldEnum.GetArrayLength())
            .Select(i => fieldEnum[i].GetString())
            .ToHashSet();
    }

    private sealed class StubTool(EntitySchema schema, string toolName) : IQueryableTool
    {
        public string ToolName { get; } = toolName;
        public string Description => "stub";
        public EntitySchema Schema { get; } = schema;
        public Task<object> ExecuteAsync(AiQueryRequest r, CancellationToken c)
            => Task.FromResult<object>(new { });
    }
}
