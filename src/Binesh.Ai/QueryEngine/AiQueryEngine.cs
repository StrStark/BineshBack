using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Top-level orchestrator. Tools call <see cref="ExecuteAsync"/> with a
/// schema, a source <see cref="IQueryable{T}"/>, and an
/// <see cref="AiQueryRequest"/>; the engine handles validate → include →
/// filter → group-or-order → page → materialize uniformly.
/// </summary>
public sealed class AiQueryEngine(CompiledSelectorCache cache)
{
    public async Task<object> ExecuteAsync<T>(
        EntitySchema schema,
        IQueryable<T> source,
        AiQueryRequest request,
        CancellationToken cancellationToken)
        where T : class
    {
        QueryValidator.Validate(schema, request);

        var query = IncludeApplicator.Apply(source, schema, request);
        query = FilterExpressionBuilder.Apply(query, schema, request.Filters);

        // ── Grouped aggregate ──────────────────────────────────────────────
        if (request.GroupBy is { Count: > 0 })
        {
            // Aggregates required-when-GroupBy is enforced by QueryValidator.
            return await AggregateExecutor.ExecuteGroupedAsync(
                query, schema, request.GroupBy, request.Select.Aggregates!, cancellationToken);
        }

        // ── Flat aggregate (no GroupBy, Mode = "aggregate") ────────────────
        if (string.Equals(request.Select.Mode, "aggregate", StringComparison.OrdinalIgnoreCase))
        {
            return await AggregateExecutor.ExecuteFlatAsync(
                query, schema, request.Select.Aggregates!, cancellationToken);
        }

        // ── List mode ──────────────────────────────────────────────────────
        query = OrderingApplicator.Apply(query, schema, request.OrderBy);
        query = PagingApplicator.Apply(query, request.Paging);

        var rows = await query.ToListAsync(cancellationToken);
        return ProjectRows(schema, request, rows);
    }

    /// <summary>
    /// Materialize-then-project. Uses <see cref="CompiledSelectorCache"/> so
    /// each (Type, FieldName) pair pays the reflection cost exactly once for
    /// the process lifetime — the legacy
    /// <c>LambdaExpression.Compile().DynamicInvoke()</c> per row is gone.
    /// </summary>
    private IReadOnlyList<IReadOnlyDictionary<string, object?>> ProjectRows<T>(
        EntitySchema schema, AiQueryRequest request, IReadOnlyList<T> rows)
        where T : class
    {
        var fieldNames = (request.Select.Fields ?? schema.Fields.Where(f => f.Selectable).Select(f => f.Name).ToList());
        var pairs = fieldNames
            .Select(name => (name, getter: cache.Get(schema.EntityType, schema.GetField(name))))
            .ToList();

        var result = new List<IReadOnlyDictionary<string, object?>>(rows.Count);
        foreach (var row in rows)
        {
            var dict = new Dictionary<string, object?>(pairs.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (name, getter) in pairs)
            {
                dict[name] = getter(row);
            }
            result.Add(dict);
        }
        return result;
    }
}
