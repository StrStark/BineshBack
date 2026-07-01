using System.Linq.Expressions;

namespace Binesh.Ai.QueryEngine;

/// <summary>
/// Translates <see cref="AiFilter"/> clauses into <c>Expression&lt;Func&lt;T, bool&gt;&gt;</c>
/// predicates against the entity, using the <see cref="FieldDescriptor.Selector"/>
/// expression tree from the schema. Predicates compose into the IQueryable
/// pipeline so EF Core renders them as SQL WHERE clauses.
/// </summary>
public static class FilterExpressionBuilder
{
    public static IQueryable<T> Apply<T>(IQueryable<T> query, EntitySchema schema, IReadOnlyList<AiFilter>? filters)
    {
        if (filters is null || filters.Count == 0) return query;

        foreach (var f in filters)
        {
            var fd = schema.GetField(f.Field);
            var typedValue = CoerceValue(f.Value, fd);
            var predicate = BuildComparison<T>(fd.Selector, f.Operator, typedValue);
            query = query.Where(predicate);
        }

        return query;
    }

    private static Expression<Func<T, bool>> BuildComparison<T>(
        LambdaExpression selector, string op, object? typedValue)
    {
        var param = selector.Parameters[0];

        // The original selector is typed as Expression<Func<TEntity, TField>>;
        // we keep its parameter as the lambda parameter and build the body
        // by walking the original. Result is Expression<Func<T, bool>>.
        var body = selector.Body;

        // Constant should match the body's static type; coerce for nullable mismatch.
        var bodyType = body.Type;
        var constant = typedValue is null
            ? (Expression)Expression.Constant(null, bodyType)
            : Expression.Constant(typedValue, MakeAssignable(bodyType, typedValue.GetType()));

        // If body is non-nullable but constant came in as the underlying type,
        // box the constant via Convert so the comparison binds.
        if (constant.Type != bodyType)
        {
            constant = Expression.Convert(constant, bodyType);
        }

        Expression comparison = op.ToLowerInvariant() switch
        {
            "eq" => Expression.Equal(body, constant),
            "ne" => Expression.NotEqual(body, constant),
            "ge" => Expression.GreaterThanOrEqual(body, constant),
            "le" => Expression.LessThanOrEqual(body, constant),
            "gt" => Expression.GreaterThan(body, constant),
            "lt" => Expression.LessThan(body, constant),
            _ => throw new InvalidOperationException($"Unsupported operator '{op}'."),
        };

        // The selector is typed as Expression<Func<TEntity, TField>> where TEntity
        // is assignable from T. Cast its parameter to T so the resulting
        // Expression<Func<T, bool>> compiles.
        var paramT = Expression.Parameter(typeof(T), param.Name);
        var replaced = ParameterReplacer.Replace(comparison, param, paramT);
        return Expression.Lambda<Func<T, bool>>(replaced, paramT);
    }

    private static Type MakeAssignable(Type bodyType, Type valueType)
    {
        // Unwrap Nullable<T> so Expression.Constant accepts a non-null underlying value.
        if (Nullable.GetUnderlyingType(bodyType) is { } underlying && underlying == valueType)
        {
            return underlying;
        }
        return valueType;
    }

    private static object? CoerceValue(object? raw, FieldDescriptor field)
    {
        if (raw is null) return null;
        var str = raw.ToString()!;

        return field.Type switch
        {
            FieldType.String => str,
            FieldType.Int32 => Convert.ToInt32(str),
            FieldType.Int64 => Convert.ToInt64(str),
            FieldType.Float => Convert.ToSingle(str),
            FieldType.Double => Convert.ToDouble(str),
            FieldType.Decimal => Convert.ToDecimal(str),
            FieldType.DateTime => DateTime.SpecifyKind(DateTime.Parse(str, System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc),
            FieldType.Bool => bool.Parse(str),
            FieldType.Enum => ParseEnum(field, str),
            _ => throw new InvalidOperationException($"Unsupported field type '{field.Type}'."),
        };
    }

    private static object ParseEnum(FieldDescriptor field, string str)
    {
        // The descriptor's selector targets an enum-typed expression body. Use
        // its return type to parse the string into the right enum instance so
        // the Expression.Equal binds against the enum, not a string.
        var enumType = field.Selector.ReturnType;
        var underlying = Nullable.GetUnderlyingType(enumType) ?? enumType;
        return Enum.Parse(underlying, str, ignoreCase: true);
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
