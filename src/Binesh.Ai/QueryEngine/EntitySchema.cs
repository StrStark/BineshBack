namespace Binesh.Ai.QueryEngine;

/// <summary>
/// A collection of <see cref="FieldDescriptor"/>s rooted at a specific
/// entity CLR type. Used by the query engine to translate AI-issued field
/// names into expression trees, validate operators/aggregates, and decide
/// which <c>.Include()</c> chains to apply.
/// </summary>
public sealed class EntitySchema
{
    public required string Name { get; init; }

    /// <summary>The CLR entity type this schema targets — e.g. <c>typeof(Sale)</c>.</summary>
    public required Type EntityType { get; init; }

    public required IReadOnlyList<FieldDescriptor> Fields { get; init; }

    /// <summary>Case-insensitive field lookup; throws if the AI requested an undeclared field.</summary>
    public FieldDescriptor GetField(string name) =>
        Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"Field '{name}' is not defined on entity '{Name}'.");

    public bool TryGetField(string name, out FieldDescriptor descriptor)
    {
        descriptor = Fields.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))!;
        return descriptor is not null;
    }
}
