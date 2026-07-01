using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Cache of compiled, type-erased getters for entity members. Replaces the
/// old code's <c>LambdaExpression.Compile().DynamicInvoke(entity)</c>
/// per-row pattern, which paid a reflection cost on every materialized row
/// and dominated CPU on large result sets.
///
/// <para>The compiled delegate adapts the original strongly-typed selector
/// (e.g. <c>(Sale s) =&gt; s.Counterparty.Person.Name</c>) into a
/// <c>Func&lt;object, object?&gt;</c> so callers can iterate a list of
/// entities and pull the value without knowing the CLR type at compile time.
/// Cached by <c>(EntityType, FieldName)</c>; instances live for the process
/// lifetime.</para>
/// </summary>
public sealed class CompiledSelectorCache
{
    private readonly ConcurrentDictionary<(Type EntityType, string FieldName), Func<object, object?>> _cache
        = new();

    /// <summary>
    /// Returns a cached, compiled getter for <paramref name="descriptor"/>'s
    /// selector against <paramref name="entityType"/>. The descriptor's
    /// selector must accept an instance of <paramref name="entityType"/>; an
    /// <see cref="InvalidOperationException"/> is thrown if it doesn't.
    /// </summary>
    public Func<object, object?> Get(Type entityType, FieldDescriptor descriptor)
    {
        var key = (entityType, descriptor.Name);
        return _cache.GetOrAdd(key, _ => Compile(entityType, descriptor));
    }

    private static Func<object, object?> Compile(Type entityType, FieldDescriptor descriptor)
    {
        var selectorParamType = descriptor.Selector.Parameters[0].Type;
        if (!selectorParamType.IsAssignableFrom(entityType))
        {
            throw new InvalidOperationException(
                $"FieldDescriptor '{descriptor.Name}' targets parameter type {selectorParamType.Name} " +
                $"but cache was asked for {entityType.Name}.");
        }

        // (object o) => (object?)((TEntity)o).<descriptor.Selector body>
        var input = Expression.Parameter(typeof(object), "o");
        var cast = Expression.Convert(input, selectorParamType);
        var inlined = ParameterReplacer.Replace(descriptor.Selector.Body, descriptor.Selector.Parameters[0], cast);
        var asObject = Expression.Convert(inlined, typeof(object));
        return Expression.Lambda<Func<object, object?>>(asObject, input).Compile();
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _from;
        private readonly Expression _to;

        private ParameterReplacer(ParameterExpression from, Expression to)
        {
            _from = from;
            _to = to;
        }

        public static Expression Replace(Expression body, ParameterExpression from, Expression to) =>
            new ParameterReplacer(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _from ? _to : base.VisitParameter(node);
    }
}
