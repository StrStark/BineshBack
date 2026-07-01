using System.Linq.Expressions;
using System.Reflection;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Applies <see cref="AiOrderBy"/> clauses to an <see cref="IQueryable{T}"/>
/// by reflecting <see cref="Queryable.OrderBy{TSource,TKey}"/> and friends —
/// the type of the sort key isn't known until runtime, so we can't dispatch
/// directly.
/// </summary>
public static class OrderingApplicator
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, EntitySchema schema, IReadOnlyList<AiOrderBy>? orderBy)
    {
        if (orderBy is null || orderBy.Count == 0) return query;

        IOrderedQueryable<T>? ordered = null;
        foreach (var o in orderBy)
        {
            var fd = schema.GetField(o.Field);
            var desc = string.Equals(o.Direction, "desc", StringComparison.OrdinalIgnoreCase);
            ordered = ApplyOne(ordered, query, fd.Selector, desc);
            query = ordered;
        }
        return query;
    }

    private static IOrderedQueryable<T> ApplyOne<T>(
        IOrderedQueryable<T>? ordered, IQueryable<T> source, LambdaExpression selector, bool desc)
    {
        var keyType = selector.ReturnType;
        var entityType = typeof(T);
        var methodName = ordered is null
            ? (desc ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy))
            : (desc ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy));

        var method = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == methodName
                         && m.GetParameters().Length == 2
                         && m.IsGenericMethodDefinition)
            .MakeGenericMethod(entityType, keyType);

        // Re-bind the selector parameter to a fresh ParameterExpression typed as T,
        // matching what Queryable's overload expects.
        var paramT = Expression.Parameter(entityType, selector.Parameters[0].Name);
        var body = ParameterReplacer.Replace(selector.Body, selector.Parameters[0], paramT);
        var typedLambda = Expression.Lambda(body, paramT);

        var target = (IQueryable<T>)(ordered ?? source);
        return (IOrderedQueryable<T>)method.Invoke(null, [target, typedLambda])!;
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly ParameterExpression _to;
        private ParameterReplacer(ParameterExpression from, ParameterExpression to) { _from = from; _to = to; }
        public static Expression Replace(Expression body, ParameterExpression from, ParameterExpression to) =>
            new ParameterReplacer(from, to).Visit(body);
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
