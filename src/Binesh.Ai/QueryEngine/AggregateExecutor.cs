using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Runs grouped and flat aggregates against an <see cref="IQueryable{T}"/>.
///
/// <para><b>The rewrite</b>: the legacy <c>AggregateExecutor.ExecuteGroupedAsync</c>
/// did <c>await query.ToListAsync()</c> and then ran <c>.GroupBy</c> over
/// in-memory rows — pulling the entire base table into application memory for
/// every panel/AI request, then losing both index access and SQL aggregate
/// pushdown.</para>
///
/// <para>The new approach issues <b>N+1 SQL queries</b> per call: one to
/// fetch the distinct group keys (or a single sentinel key for flat mode),
/// then one per aggregate to fetch (key, value) pairs. Each query is a clean
/// <c>GROUP BY … SELECT key, agg</c> in SQL. We merge by key in memory at
/// the end. N is typically &lt;= 5 for a single AI tool call.</para>
///
/// <para>Flat mode (<c>Mode = "aggregate"</c> with no <c>GroupBy</c>) reuses
/// the grouped path with a constant byte key, so both code paths share the
/// same expression-tree builder.</para>
/// </summary>
public static class AggregateExecutor
{
    /// <summary>
    /// Flat aggregates. Returns a single <see cref="IReadOnlyDictionary{TKey,TValue}"/>
    /// keyed by <see cref="AiAggregate.Alias"/>. Implemented by issuing the
    /// same grouped queries with a constant key, then unwrapping the single row.
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, object?>> ExecuteFlatAsync<T>(
        IQueryable<T> query,
        EntitySchema schema,
        IReadOnlyList<AiAggregate> aggregates,
        CancellationToken cancellationToken)
        where T : class
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var agg in aggregates)
        {
            // Constant key projection: GroupBy(_ => (byte)0).Select(g => new(0, agg))
            var pair = await ComputeAggregateAsync<T, byte>(
                query, schema, agg, ConstantKey<T, byte>(0), cancellationToken);
            result[agg.Alias] = pair.value;
        }
        return result;
    }

    /// <summary>
    /// Grouped aggregates. Issues one SQL key-fetch query plus one per
    /// aggregate, then merges by key.
    /// </summary>
    public static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecuteGroupedAsync<T>(
        IQueryable<T> query,
        EntitySchema schema,
        IReadOnlyList<AiGroupBy> groupBy,
        IReadOnlyList<AiAggregate> aggregates,
        CancellationToken cancellationToken)
        where T : class
    {
        if (groupBy.Count != 1)
        {
            throw new InvalidOperationException("Only single-field GroupBy is currently supported.");
        }

        var groupField = schema.GetField(groupBy[0].Field);
        var keyType = groupField.Selector.ReturnType;
        var keySelector = RebindToT<T>(groupField.Selector);

        // Distinct key set — pulled as its own SQL DISTINCT so we still know
        // every group key that has at least one row, even before any aggregate
        // has run.
        var keys = await DistinctKeysAsync<T>(query, keySelector, keyType, cancellationToken);

        var byKey = new Dictionary<object, Dictionary<string, object?>>(new BoxedEquality());
        foreach (var key in keys.Where(k => k is not null))
        {
            byKey[key!] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                [groupField.Name] = key,
            };
        }

        foreach (var agg in aggregates)
        {
            var pairs = await ComputeGroupedAggregateAsync<T>(
                query, schema, agg, keySelector, keyType, cancellationToken);

            foreach (var (key, value) in pairs)
            {
                if (key is null) continue;
                if (!byKey.TryGetValue(key, out var row))
                {
                    row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    {
                        [groupField.Name] = key,
                    };
                    byKey[key] = row;
                }
                row[agg.Alias] = value;
            }
        }

        return byKey.Values.ToList();
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private static LambdaExpression ConstantKey<T, TKey>(TKey value)
    {
        var p = Expression.Parameter(typeof(T), "x");
        var c = Expression.Constant(value, typeof(TKey));
        return Expression.Lambda(c, p);
    }

    /// <summary>Internal flat path delegates here through a constant key.</summary>
    private static async Task<(object? key, object? value)> ComputeAggregateAsync<T, TKey>(
        IQueryable<T> query,
        EntitySchema schema,
        AiAggregate agg,
        LambdaExpression constKeySelector,
        CancellationToken ct)
        where T : class
    {
        var pairs = await ComputeGroupedAggregateAsync<T>(
            query, schema, agg, constKeySelector, typeof(TKey), ct);
        return pairs.Count == 0
            ? (default(TKey), null)
            : (pairs[0].key, pairs[0].value);
    }

    private static async Task<List<object?>> DistinctKeysAsync<T>(
        IQueryable<T> query, LambdaExpression keySelector, Type keyType, CancellationToken ct)
        where T : class
    {
        var select = QueryableSelectMethod(typeof(T), keyType);
        var projected = (IQueryable)select.Invoke(null, [query, keySelector])!;

        var distinct = QueryableDistinctMethod(keyType);
        var distinctQuery = (IQueryable)distinct.Invoke(null, [projected])!;

        var toList = EFCoreToListAsyncMethod(keyType);
        var task = (Task)toList.Invoke(null, [distinctQuery, ct])!;
        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty(nameof(Task<int>.Result))!;
        var list = (System.Collections.IEnumerable)resultProp.GetValue(task)!;

        var keys = new List<object?>();
        foreach (var k in list) keys.Add(k);
        return keys;
    }

    private static async Task<List<(object? key, object? value)>> ComputeGroupedAggregateAsync<T>(
        IQueryable<T> query,
        EntitySchema schema,
        AiAggregate agg,
        LambdaExpression keySelector,
        Type keyType,
        CancellationToken ct)
        where T : class
    {
        var func = agg.Function.ToLowerInvariant();

        // Build: query.GroupBy(keySelector).Select(g => new KeyAndValue<TKey, TValue>(g.Key, <agg>))
        var groupBy = QueryableGroupByMethod(typeof(T), keyType);
        var grouped = (IQueryable)groupBy.Invoke(null, [query, keySelector])!;
        var groupingType = typeof(IGrouping<,>).MakeGenericType(keyType, typeof(T));

        var gParam = Expression.Parameter(groupingType, "g");
        var keyAccess = Expression.Property(gParam, "Key");

        Expression valueExpr;
        Type valueType;
        if (func == "count")
        {
            var countMethod = typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(Enumerable.Count)
                            && m.GetParameters().Length == 1
                            && m.IsGenericMethodDefinition)
                .MakeGenericMethod(typeof(T));
            valueExpr = Expression.Call(countMethod, gParam);
            valueType = typeof(int);
        }
        else
        {
            var fd = schema.GetField(agg.Field);
            var elementLambda = RebindToT<T>(fd.Selector);
            valueType = ResolveAggregateReturnType(func, elementLambda.ReturnType);
            var enumMethod = ResolveEnumerableAggregate(func, typeof(T), elementLambda.ReturnType);
            valueExpr = Expression.Call(enumMethod, gParam, elementLambda);
        }

        var rowOpenType = typeof(KeyAndValue<,>);
        var rowType = rowOpenType.MakeGenericType(keyType, valueType);
        var rowCtor = rowType.GetConstructor([keyType, valueType])!;
        var rowExpr = Expression.New(rowCtor, keyAccess, valueExpr);
        var projLambda = Expression.Lambda(rowExpr, gParam);

        var selectMethod = QueryableSelectMethod(groupingType, rowType);
        var projected = (IQueryable)selectMethod.Invoke(null, [grouped, projLambda])!;

        var toList = EFCoreToListAsyncMethod(rowType);
        var task = (Task)toList.Invoke(null, [projected, ct])!;
        await task.ConfigureAwait(false);
        var resultProp = task.GetType().GetProperty(nameof(Task<int>.Result))!;
        var list = (System.Collections.IEnumerable)resultProp.GetValue(task)!;

        var pairs = new List<(object? key, object? value)>();
        var keyProp = rowType.GetProperty("Key")!;
        var valProp = rowType.GetProperty("Value")!;
        foreach (var row in list)
        {
            pairs.Add((keyProp.GetValue(row), valProp.GetValue(row)));
        }
        return pairs;
    }

    private static MethodInfo ResolveEnumerableAggregate(string func, Type tSource, Type tElement)
    {
        var name = func switch
        {
            "sum" => nameof(Enumerable.Sum),
            "avg" => nameof(Enumerable.Average),
            "min" => nameof(Enumerable.Min),
            "max" => nameof(Enumerable.Max),
            _ => throw new InvalidOperationException($"Unsupported aggregate '{func}'."),
        };

        // Sum and Average are typed by element (long, double, decimal, float, int)
        // and have an overload taking (IEnumerable<TSource>, Func<TSource, TResult>).
        // Min/Max are generic in TSource and have a generic key overload.

        if (func is "sum" or "avg")
        {
            // Strip nullable for matching.
            var nonNullable = Nullable.GetUnderlyingType(tElement) ?? tElement;
            return typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == name && m.GetParameters().Length == 2)
                .Where(m =>
                {
                    var ps = m.GetParameters();
                    if (!ps[1].ParameterType.IsGenericType) return false;
                    var def = ps[1].ParameterType.GetGenericTypeDefinition();
                    if (def != typeof(Func<,>)) return false;
                    var ret = ps[1].ParameterType.GetGenericArguments()[1];
                    return ret == tElement || ret == nonNullable;
                })
                .Select(m => m.IsGenericMethodDefinition ? m.MakeGenericMethod(tSource) : m)
                .First();
        }

        // Min / Max — generic in <TSource, TResult>
        return typeof(Enumerable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == name
                        && m.GetParameters().Length == 2
                        && m.IsGenericMethodDefinition
                        && m.GetGenericArguments().Length == 2)
            .MakeGenericMethod(tSource, tElement);
    }

    private static Type ResolveAggregateReturnType(string func, Type tElement)
    {
        var nonNullable = Nullable.GetUnderlyingType(tElement) ?? tElement;
        return func switch
        {
            "sum" => tElement,
            "avg" when nonNullable == typeof(int) || nonNullable == typeof(long) => typeof(double),
            "avg" when nonNullable == typeof(float) => typeof(float),
            "avg" when nonNullable == typeof(double) => typeof(double),
            "avg" when nonNullable == typeof(decimal) => typeof(decimal),
            "avg" => typeof(double),
            "min" or "max" => tElement,
            _ => throw new InvalidOperationException($"Unsupported aggregate '{func}'."),
        };
    }

    private static MethodInfo QueryableSelectMethod(Type tSource, Type tResult) =>
        typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Queryable.Select)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType.IsGenericType
                        && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                        && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>))
            .MakeGenericMethod(tSource, tResult);

    private static MethodInfo QueryableDistinctMethod(Type tSource) =>
        typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Queryable.Distinct)
                        && m.GetParameters().Length == 1)
            .MakeGenericMethod(tSource);

    private static MethodInfo QueryableGroupByMethod(Type tSource, Type tKey) =>
        typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(Queryable.GroupBy)
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType.IsGenericType
                        && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                        && m.GetParameters()[1].ParameterType.GetGenericArguments()[0].GetGenericTypeDefinition() == typeof(Func<,>))
            .MakeGenericMethod(tSource, tKey);

    private static MethodInfo EFCoreToListAsyncMethod(Type tSource) =>
        typeof(EntityFrameworkQueryableExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync)
                        && m.GetParameters().Length == 2)
            .MakeGenericMethod(tSource);

    private static LambdaExpression RebindToT<T>(LambdaExpression source)
    {
        var paramT = Expression.Parameter(typeof(T), source.Parameters[0].Name);
        var body = new ParameterReplacer(source.Parameters[0], paramT).Visit(source.Body);
        return Expression.Lambda(body!, paramT);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }

    /// <summary>Equality for boxed key values so <c>Dictionary&lt;object&gt;</c> works for value-types.</summary>
    private sealed class BoxedEquality : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) =>
            (x is null && y is null) || (x is not null && y is not null && x.Equals(y));
        public int GetHashCode(object obj) => obj?.GetHashCode() ?? 0;
    }
}

/// <summary>SQL-projection holder used by <see cref="AggregateExecutor"/>.</summary>
public sealed class KeyAndValue<TKey, TValue>(TKey key, TValue value)
{
    public TKey Key { get; init; } = key;
    public TValue Value { get; init; } = value;
}
