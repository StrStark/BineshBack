using System.Text;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Tools;

namespace Binesh.Ai.Prompts;

/// <summary>
/// Builds the per-conversation system-prompt addendum from every registered
/// <see cref="IQueryableTool"/>. Renders one markdown table per entity
/// listing the fields the AI may reference. The actual per-tool JSON schema
/// is the LLM's hard constraint — this prompt is the soft guide.
/// </summary>
public static class QueryPromptBuilder
{
    public static string Build(QueryToolRegistry registry)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("SUPPORTED ENTITIES");
        foreach (var tool in registry.All)
        {
            sb.AppendLine($"- \"{tool.Schema.Name}\" → call tool \"{tool.ToolName}\"");
        }
        sb.AppendLine();

        foreach (var tool in registry.All)
        {
            var schema = tool.Schema;
            sb.AppendLine($"ENTITY: \"{schema.Name}\"");
            sb.AppendLine();
            sb.AppendLine("| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |");
            sb.AppendLine("|-------|------|------------|------------|-----------|-----------|--------------|");
            foreach (var f in schema.Fields)
            {
                var filterable = f.AllowedOperators.Count > 0
                    ? $"yes ({string.Join(", ", f.AllowedOperators)})"
                    : "no";
                var aggregatable = f.Aggregatable && f.AllowedAggregates.Count > 0
                    ? $"yes ({string.Join(", ", f.AllowedAggregates)})"
                    : "no";
                sb.AppendLine(
                    $"| {f.Name} | {f.Type.ToString().ToLowerInvariant()} | {filterable} " +
                    $"| {(f.Selectable ? "yes" : "no")} | {(f.Orderable ? "yes" : "no")} " +
                    $"| {(f.Groupable ? "yes" : "no")} | {aggregatable} |");
            }

            var enumFields = schema.Fields.Where(f => f.Type == FieldType.Enum && f.AllowedValues is not null);
            foreach (var f in enumFields)
            {
                sb.AppendLine();
                sb.AppendLine($"{schema.Name}.{f.Name} allowed values: {string.Join(", ", f.AllowedValues!)}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
