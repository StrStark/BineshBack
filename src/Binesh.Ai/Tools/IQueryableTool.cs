using Binesh.Ai.QueryEngine;

namespace Binesh.Ai.Tools;

/// <summary>
/// Central abstraction for any entity the AI can query.
///
/// <para>To make a new entity queryable: declare an <see cref="EntitySchema"/>
/// under <c>Binesh.Ai/Schemas/</c> and register it in
/// <see cref="Binesh.Ai.DependencyInjection.AddBineshAi"/>, then add a tool
/// derived from <see cref="QueryableToolBase{T}"/>. The system prompt
/// section, ChatTool definition, and dispatch routing are all derived from
/// this interface — no switch statements anywhere.</para>
/// </summary>
public interface IQueryableTool
{
    /// <summary>
    /// Exact function name OpenAI uses when calling this tool. Convention:
    /// <c>"query_{entity_name_lowercase}"</c> e.g. <c>"query_sale"</c>. Must
    /// be unique across all registered tools.
    /// </summary>
    string ToolName { get; }

    /// <summary>Description shown to the AI. Be specific — the AI picks the right tool based on this.</summary>
    string Description { get; }

    /// <summary>The schema that defines every queryable field.</summary>
    EntitySchema Schema { get; }

    /// <summary>Executes an AI-generated query and returns a serializable result.</summary>
    Task<object> ExecuteAsync(AiQueryRequest request, CancellationToken cancellationToken);
}
