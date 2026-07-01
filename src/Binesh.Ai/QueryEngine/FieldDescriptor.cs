using System.Linq.Expressions;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Describes one field the AI is allowed to reference on an entity. The
/// <see cref="Selector"/> stays a <see cref="LambdaExpression"/> (not a
/// compiled delegate) so EF Core can decompose it into SQL. Supports
/// navigation property access like <c>(Sale s) =&gt; s.Counterparty.Person.Name</c>
/// as long as the navigation paths are listed in <see cref="RequiredIncludes"/>.
/// </summary>
public sealed class FieldDescriptor
{
    /// <summary>The name the AI uses — must match what the prompt advertises.</summary>
    public required string Name { get; init; }

    public required FieldType Type { get; init; }

    /// <summary>
    /// Expression-tree selector. EF Core walks this into SQL; do NOT pre-compile.
    /// The parameter type is whatever entity this descriptor targets; consumers
    /// downcast via reflection when grouping handlers by entity.
    /// </summary>
    public required LambdaExpression Selector { get; init; }

    /// <summary>
    /// EF Core navigation paths that must be <c>.Include()</c>'d when this
    /// field appears in a projection or filter. Use dot notation for nested
    /// paths (<c>"Counterparty.Person.Region"</c>). Leave empty for flat
    /// fields on the root entity.
    /// </summary>
    public string[] RequiredIncludes { get; init; } = [];

    /// <summary>Filter operators the AI may use against this field. Empty = not filterable.</summary>
    public HashSet<string> AllowedOperators { get; init; } = ["eq", "ne", "ge", "le"];

    public bool Orderable { get; init; } = true;
    public bool Groupable { get; init; } = false;
    public bool Selectable { get; init; } = true;
    public bool Aggregatable { get; init; } = false;

    /// <summary>Aggregate functions the AI may use against this field. Drives prompt + runtime validation.</summary>
    public HashSet<string> AllowedAggregates { get; init; } = [];

    /// <summary>For <see cref="FieldType.Enum"/> fields: the exact string values the AI may use.</summary>
    public IReadOnlyList<string>? AllowedValues { get; init; }
}
