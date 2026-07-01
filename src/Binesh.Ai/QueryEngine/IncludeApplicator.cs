using Microsoft.EntityFrameworkCore;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Walks the request, collects every <see cref="FieldDescriptor.RequiredIncludes"/>
/// path that's actually referenced (in Filters / Select / OrderBy / GroupBy / Aggregates),
/// dedups by removing any path that's a prefix of a longer path, and applies
/// <c>Include(string)</c> chains. EF Core resolves the dotted strings into
/// Include/ThenInclude calls.
///
/// Callers never touch this code when adding new fields — they just declare
/// <c>RequiredIncludes</c> on the <see cref="FieldDescriptor"/>.
/// </summary>
public static class IncludeApplicator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, EntitySchema schema, AiQueryRequest request)
        where T : class
    {
        var paths = CollectPaths(schema, request);
        foreach (var path in paths)
        {
            query = query.Include(path);
        }
        return query;
    }

    public static IReadOnlyList<string> CollectPaths(EntitySchema schema, AiQueryRequest request)
    {
        var raw = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? fieldName)
        {
            if (string.IsNullOrWhiteSpace(fieldName)) return;
            if (!schema.TryGetField(fieldName, out var fd)) return;
            foreach (var p in fd.RequiredIncludes)
            {
                raw.Add(p);
            }
        }

        foreach (var f in request.Filters ?? []) Add(f.Field);
        foreach (var f in request.Select.Fields ?? []) Add(f);
        foreach (var a in request.Select.Aggregates ?? []) Add(a.Field);
        foreach (var o in request.OrderBy ?? []) Add(o.Field);
        foreach (var g in request.GroupBy ?? []) Add(g.Field);

        return RemoveRedundantPaths(raw);
    }

    /// <summary>
    /// Drops every path that's a strict prefix of another path in the set
    /// (case-insensitive, dot-segment aware). <c>Include("a.b.c")</c> already
    /// pulls <c>a</c> and <c>a.b</c>, so retaining the shorter ones is wasted
    /// metadata.
    /// </summary>
    private static IReadOnlyList<string> RemoveRedundantPaths(HashSet<string> paths)
    {
        return paths
            .Where(p => !paths.Any(other =>
                !ReferenceEquals(p, other)
                && other.Length > p.Length
                && other.StartsWith(p + ".", StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
