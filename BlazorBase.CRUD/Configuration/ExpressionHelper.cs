using System.Linq.Expressions;
using System.Reflection;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Extracts property names from lambda expressions.
/// </summary>
public static class ExpressionHelper
{
    public static string GetPropertyName<TModel>(Expression<Func<TModel, object?>> expression)
    {
        var member = GetMemberExpression(expression.Body);

        if (member is null)
            throw new ArgumentException("Expression must reference a property.");

        return GetPropertyPath(member);
    }

    public static string GetPropertyName(LambdaExpression expression)
    {
        var member = GetMemberExpression(expression.Body);

        if (member is null)
            throw new ArgumentException("Expression must reference a property.");

        return GetPropertyPath(member);
    }

    public static string GetPropertyPath(Expression expression)
    {
        var member = GetMemberExpression(expression);

        if (member is null)
            throw new ArgumentException("Expression must reference a property.");

        var parts = new List<string>();
        var current = member;

        while (current is not null)
        {
            parts.Add(current.Member.Name);
            current = current.Expression as MemberExpression;
        }

        parts.Reverse();
        return string.Join('.', parts);
    }

    public static PropertyInfo GetPropertyInfo<TModel>(Expression<Func<TModel, object?>> expression)
    {
        var member = GetMemberExpression(expression.Body);

        if (member?.Member is PropertyInfo propertyInfo)
            return propertyInfo;

        throw new ArgumentException("Expression must reference a property.");
    }

    private static MemberExpression? GetMemberExpression(Expression expression)
    {
        if (expression is MemberExpression member)
            return member;

        if (expression is UnaryExpression { Operand: MemberExpression unaryMember })
            return unaryMember;

        return null;
    }
}
