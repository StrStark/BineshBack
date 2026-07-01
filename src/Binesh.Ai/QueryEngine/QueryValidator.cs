namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Validates an <see cref="AiQueryRequest"/> against an <see cref="EntitySchema"/>
/// before it touches the database. Accumulates every violation into one
/// <see cref="InvalidOperationException"/> so the LLM gets a single corrective
/// message instead of N round-trips of one-error-at-a-time.
/// </summary>
public static class QueryValidator
{
    public static void Validate(EntitySchema schema, AiQueryRequest request)
    {
        var errors = new List<string>();

        ValidateMode(request, errors);
        ValidateFields(schema, request, errors);
        ValidateFilters(schema, request, errors);
        ValidateAggregates(schema, request, errors);
        ValidateGroupBy(schema, request, errors);
        ValidateOrderBy(schema, request, errors);
        ValidatePaging(request, errors);

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }

    private static void ValidateMode(AiQueryRequest request, List<string> errors)
    {
        var mode = request.Select.Mode?.ToLowerInvariant();
        if (mode is not ("list" or "aggregate"))
        {
            errors.Add($"Select.Mode must be 'list' or 'aggregate', got '{request.Select.Mode}'.");
        }

        if (mode == "aggregate" && (request.Select.Aggregates is null || request.Select.Aggregates.Count == 0))
        {
            errors.Add("Select.Aggregates is required when Mode is 'aggregate'.");
        }

        if (request.GroupBy is { Count: > 0 } && (request.Select.Aggregates is null || request.Select.Aggregates.Count == 0))
        {
            errors.Add("Aggregates are required when GroupBy is present.");
        }
    }

    private static void ValidateFields(EntitySchema schema, AiQueryRequest request, List<string> errors)
    {
        foreach (var name in request.Select.Fields ?? [])
        {
            if (!schema.TryGetField(name, out var f))
            {
                errors.Add($"Select field '{name}' is not defined on entity '{schema.Name}'.");
                continue;
            }
            if (!f.Selectable)
            {
                errors.Add($"Field '{name}' is not selectable.");
            }
        }
    }

    private static void ValidateFilters(EntitySchema schema, AiQueryRequest request, List<string> errors)
    {
        foreach (var f in request.Filters ?? [])
        {
            if (!schema.TryGetField(f.Field, out var fd))
            {
                errors.Add($"Filter field '{f.Field}' is not defined on entity '{schema.Name}'.");
                continue;
            }
            var op = f.Operator?.ToLowerInvariant() ?? "";
            if (!fd.AllowedOperators.Contains(op))
            {
                errors.Add(
                    $"Operator '{f.Operator}' is not allowed on field '{f.Field}'. " +
                    $"Allowed: {string.Join(", ", fd.AllowedOperators)}");
            }
            if (fd.Type == FieldType.Enum && fd.AllowedValues is not null)
            {
                var strVal = f.Value?.ToString() ?? "";
                if (!fd.AllowedValues.Any(v => v.Equals(strVal, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(
                        $"Value '{strVal}' is not valid for enum field '{f.Field}'. " +
                        $"Allowed: {string.Join(", ", fd.AllowedValues)}");
                }
            }
        }
    }

    private static void ValidateAggregates(EntitySchema schema, AiQueryRequest request, List<string> errors)
    {
        foreach (var a in request.Select.Aggregates ?? [])
        {
            if (string.IsNullOrWhiteSpace(a.Alias))
            {
                errors.Add($"Aggregate on '{a.Field}' is missing an Alias.");
            }

            // Count is allowed without a field type check (counts rows, not a specific value).
            if (a.Function.Equals("count", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!schema.TryGetField(a.Field, out var fd))
            {
                errors.Add($"Aggregate field '{a.Field}' is not defined on entity '{schema.Name}'.");
                continue;
            }
            if (!fd.Aggregatable)
            {
                errors.Add($"Field '{a.Field}' is not aggregatable.");
                continue;
            }
            if (!fd.AllowedAggregates.Contains(a.Function.ToLowerInvariant()))
            {
                errors.Add(
                    $"Aggregate '{a.Function}' is not allowed on field '{a.Field}'. " +
                    $"Allowed: {string.Join(", ", fd.AllowedAggregates)}");
            }
        }
    }

    private static void ValidateGroupBy(EntitySchema schema, AiQueryRequest request, List<string> errors)
    {
        foreach (var g in request.GroupBy ?? [])
        {
            if (!schema.TryGetField(g.Field, out var fd))
            {
                errors.Add($"GroupBy field '{g.Field}' is not defined on entity '{schema.Name}'.");
                continue;
            }
            if (!fd.Groupable)
            {
                errors.Add($"Field '{g.Field}' is not groupable.");
            }
        }
    }

    private static void ValidateOrderBy(EntitySchema schema, AiQueryRequest request, List<string> errors)
    {
        foreach (var o in request.OrderBy ?? [])
        {
            if (!schema.TryGetField(o.Field, out var fd))
            {
                errors.Add(
                    $"OrderBy field '{o.Field}' is not defined on entity '{schema.Name}'. " +
                    $"Aggregate aliases cannot be used in OrderBy.");
                continue;
            }
            if (!fd.Orderable)
            {
                errors.Add($"Field '{o.Field}' is not orderable.");
            }
            var dir = o.Direction?.ToLowerInvariant();
            if (dir is not (null or "asc" or "desc"))
            {
                errors.Add($"OrderBy direction must be 'asc' or 'desc', got '{o.Direction}'.");
            }
        }
    }

    private static void ValidatePaging(AiQueryRequest request, List<string> errors)
    {
        if (request.Paging is null) return;
        if (request.Paging.Skip < 0) errors.Add("Paging.Skip must be >= 0.");
        if (request.Paging.Take < 1) errors.Add("Paging.Take must be >= 1.");
    }
}
