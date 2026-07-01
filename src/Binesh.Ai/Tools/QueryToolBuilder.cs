using System.Text.Json;
using OpenAI.Chat;

namespace Binesh.Ai.Tools;

/// <summary>
/// Builds an OpenAI <see cref="ChatTool"/> definition for a given
/// <see cref="IQueryableTool"/>. Every tool gets its OWN function definition
/// with a field-name enum scoped to that tool's schema only.
///
/// <para><b>Legacy bug fix.</b> The old code's <c>QueryToolBuilder</c>
/// emitted ONE function definition with a union of every schema's fields,
/// so the LLM could (and did) try to filter <c>Sales</c> rows by
/// <c>CustomerType</c> — a Customer field — which produced runtime
/// "field not on entity" exceptions and confused retries. Per-tool schemas
/// make those mismatches impossible: the function definition itself rejects
/// the call before it ever reaches the engine.</para>
/// </summary>
public static class QueryToolBuilder
{
    // NOT JsonSerializerDefaults.Web — that camelCases property names and the
    // AiQueryRequest record uses PascalCase identifiers we must preserve.
    private static readonly JsonSerializerOptions JsonOpts = new();

    public static ChatTool Build(IQueryableTool tool)
    {
        var schema = tool.Schema;
        var fieldNames = schema.Fields.Select(f => f.Name).ToList();

        var parameters = new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = new Dictionary<string, object?>
            {
                ["Entity"] = new Dictionary<string, object?>
                {
                    ["type"] = "string",
                    ["enum"] = new[] { schema.Name },
                },
                ["Select"] = new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["Mode"] = new { type = "string", @enum = new[] { "list", "aggregate" } },
                        ["Fields"] = new
                        {
                            type = new[] { "array", "null" },
                            items = new { type = "string", @enum = fieldNames },
                        },
                        ["Aggregates"] = new
                        {
                            type = new[] { "array", "null" },
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    Function = new { type = "string", @enum = new[] { "count", "sum", "avg", "min", "max" } },
                                    Field = new { type = "string", @enum = fieldNames },
                                    Alias = new { type = "string" },
                                },
                                required = new[] { "Function", "Field", "Alias" },
                                additionalProperties = false,
                            },
                        },
                    },
                    ["required"] = new[] { "Mode" },
                    ["additionalProperties"] = false,
                },
                ["Filters"] = new
                {
                    type = new[] { "array", "null" },
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            Field = new { type = "string", @enum = fieldNames },
                            Operator = new { type = "string", @enum = new[] { "eq", "ne", "ge", "le", "gt", "lt" } },
                            Value = new { type = new[] { "string", "number", "boolean", "null" } },
                        },
                        required = new[] { "Field", "Operator", "Value" },
                        additionalProperties = false,
                    },
                },
                ["GroupBy"] = new
                {
                    type = new[] { "array", "null" },
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            Field = new { type = "string", @enum = fieldNames },
                        },
                        required = new[] { "Field" },
                        additionalProperties = false,
                    },
                },
                ["OrderBy"] = new
                {
                    type = new[] { "array", "null" },
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            Field = new { type = "string", @enum = fieldNames },
                            Direction = new { type = "string", @enum = new[] { "asc", "desc" } },
                        },
                        required = new[] { "Field", "Direction" },
                        additionalProperties = false,
                    },
                },
                ["Paging"] = new
                {
                    type = new[] { "object", "null" },
                    properties = new
                    {
                        Skip = new { type = "integer", minimum = 0 },
                        Take = new { type = "integer", minimum = 1 },
                    },
                    required = new[] { "Skip", "Take" },
                    additionalProperties = false,
                },
            },
            ["required"] = new[] { "Entity", "Select" },
            ["additionalProperties"] = false,
        };

        var json = JsonSerializer.Serialize(parameters, JsonOpts);
        return ChatTool.CreateFunctionTool(
            functionName: tool.ToolName,
            functionDescription: tool.Description,
            functionParameters: BinaryData.FromString(json));
    }

    public static IEnumerable<ChatTool> BuildAll(QueryToolRegistry registry) =>
        registry.All.Select(Build);

    /// <summary>For tests — returns the JSON schema string the ChatTool exposes. Same content as <see cref="Build"/>.</summary>
    public static string BuildParametersJson(IQueryableTool tool)
    {
        var chatTool = Build(tool);
        return chatTool.FunctionParameters.ToString();
    }
}
