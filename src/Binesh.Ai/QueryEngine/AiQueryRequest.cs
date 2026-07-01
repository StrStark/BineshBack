namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Canonical input shape the AI emits when invoking a query tool. Shape is
/// fixed so the per-tool JSON schema (12c) can advertise it as a function
/// definition to the LLM. Matches the legacy <c>AiQueryRequest</c> contract
/// for prompt-compatibility — see 12a notes in CHANGES.md.
/// </summary>
public sealed record AiQueryRequest(
    string Entity,
    AiSelect Select,
    IReadOnlyList<AiFilter>? Filters,
    IReadOnlyList<AiGroupBy>? GroupBy,
    IReadOnlyList<AiOrderBy>? OrderBy,
    AiPaging? Paging);

/// <summary>
/// <c>Mode</c> is "list" (return rows projected to the requested fields)
/// or "aggregate" (return scalars / grouped aggregates).
/// </summary>
public sealed record AiSelect(
    string Mode,
    IReadOnlyList<string>? Fields,
    IReadOnlyList<AiAggregate>? Aggregates);

public sealed record AiAggregate(string Function, string Field, string Alias);

public sealed record AiFilter(string Field, string Operator, object? Value);

public sealed record AiGroupBy(string Field);

public sealed record AiOrderBy(string Field, string Direction);

public sealed record AiPaging(int Skip, int Take);
