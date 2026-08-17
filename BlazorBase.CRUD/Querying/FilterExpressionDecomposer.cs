using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Querying;

/// <summary>
/// Decomposes a predicate expression into a <see cref="FilterDescriptor"/> tree
/// supporting AND/OR groups and leaf conditions.
/// </summary>
public static class FilterExpressionDecomposer
{
    public static FilterDescriptor Decompose<T>(Expression<Func<T, bool>> predicate)
    {
        return Visit(predicate.Body, predicate.Parameters[0]);
    }

    private static FilterDescriptor Visit(Expression expression, ParameterExpression parameter)
    {
        return expression switch
        {
            BinaryExpression binary => VisitBinary(binary, parameter),
            MethodCallExpression methodCall => VisitMethodCall(methodCall),
            UnaryExpression { NodeType: ExpressionType.Not } unary => VisitNot(unary, parameter),
            MemberExpression member when member.Type == typeof(bool) => VisitBoolMember(member, true),
            _ => throw new NotSupportedException($"Expression type '{expression.NodeType}' is not supported in filter decomposition.")
        };
    }

    private static FilterDescriptor VisitBinary(BinaryExpression binary, ParameterExpression parameter)
    {
        if (binary.NodeType == ExpressionType.AndAlso)
            return VisitLogical(binary, FilterLogic.And, parameter);

        if (binary.NodeType == ExpressionType.OrElse)
            return VisitLogical(binary, FilterLogic.Or, parameter);

        return VisitComparison(binary);
    }

    private static FilterDescriptor VisitLogical(BinaryExpression binary, FilterLogic logic, ParameterExpression parameter)
    {
        var left = Visit(binary.Left, parameter);
        var right = Visit(binary.Right, parameter);

        var children = new List<FilterDescriptor>();
        FlattenGroup(left, logic, children);
        FlattenGroup(right, logic, children);

        return new FilterDescriptor { Logic = logic, Filters = children };
    }

    private static void FlattenGroup(FilterDescriptor descriptor, FilterLogic logic, List<FilterDescriptor> target)
    {
        if (descriptor.IsGroup && descriptor.Logic == logic)
            target.AddRange(descriptor.Filters!);
        else
            target.Add(descriptor);
    }

    private static FilterDescriptor VisitComparison(BinaryExpression binary)
    {
        var (propertyPath, value) = ExtractPropertyAndValue(binary.Left, binary.Right);

        if (value is null && binary.NodeType is ExpressionType.Equal)
            return new FilterDescriptor { PropertyName = propertyPath, Operator = FilterOperator.IsNull };

        if (value is null && binary.NodeType is ExpressionType.NotEqual)
            return new FilterDescriptor { PropertyName = propertyPath, Operator = FilterOperator.IsNotNull };

        var op = binary.NodeType switch
        {
            ExpressionType.Equal => FilterOperator.Equals,
            ExpressionType.NotEqual => FilterOperator.NotEquals,
            ExpressionType.GreaterThan => FilterOperator.GreaterThan,
            ExpressionType.GreaterThanOrEqual => FilterOperator.GreaterThanOrEqual,
            ExpressionType.LessThan => FilterOperator.LessThan,
            ExpressionType.LessThanOrEqual => FilterOperator.LessThanOrEqual,
            _ => throw new NotSupportedException($"Binary operator '{binary.NodeType}' is not supported.")
        };

        return new FilterDescriptor { PropertyName = propertyPath, Operator = op, Value = value };
    }

    private static FilterDescriptor VisitMethodCall(MethodCallExpression methodCall)
    {
        if (methodCall.Method.Name == "Contains" && IsCollectionContainsCall(methodCall, out var collectionValue, out var memberArgument))
            return new FilterDescriptor
            {
                PropertyName = ExpressionHelper.GetPropertyPath(memberArgument!),
                Operator = FilterOperator.In,
                Value = collectionValue
            };

        if (methodCall.Object is not MemberExpression member)
            throw new NotSupportedException($"Method call '{methodCall.Method.Name}' on non-member is not supported.");

        var op = methodCall.Method.Name switch
        {
            "Contains" => FilterOperator.Contains,
            "StartsWith" => FilterOperator.StartsWith,
            "EndsWith" => FilterOperator.EndsWith,
            _ => throw new NotSupportedException($"Method '{methodCall.Method.Name}' is not supported in filter decomposition.")
        };

        var value = ResolveValue(methodCall.Arguments[0]);
        var propertyPath = ExpressionHelper.GetPropertyPath(member);

        return new FilterDescriptor { PropertyName = propertyPath, Operator = op, Value = value };
    }

    private static bool IsCollectionContainsCall(MethodCallExpression methodCall, out object? collectionValue, out MemberExpression? memberArgument)
    {
        collectionValue = null;
        memberArgument = null;

        if (methodCall.Method.DeclaringType == typeof(string))
            return false;

        Expression collectionExpr;
        Expression itemExpr;

        if (methodCall.Object is null && methodCall.Arguments.Count == 2)
        {
            collectionExpr = methodCall.Arguments[0];
            itemExpr = methodCall.Arguments[1];
        }
        else if (methodCall.Object is not null && methodCall.Arguments.Count == 1)
        {
            collectionExpr = methodCall.Object;
            itemExpr = methodCall.Arguments[0];
        }
        else
        {
            return false;
        }

        if (itemExpr is UnaryExpression unary)
            itemExpr = unary.Operand;

        if (itemExpr is not MemberExpression memberExpr || !IsMemberChain(memberExpr))
            return false;

        memberArgument = memberExpr;
        collectionValue = ResolveValue(collectionExpr);
        return true;
    }

    private static FilterDescriptor VisitNot(UnaryExpression unary, ParameterExpression parameter)
    {
        if (unary.Operand is MemberExpression member && member.Type == typeof(bool))
            return VisitBoolMember(member, false);

        var inner = Visit(unary.Operand, parameter);

        if (!inner.IsGroup && inner.Operator == FilterOperator.Equals)
        {
            inner.Operator = FilterOperator.NotEquals;
            return inner;
        }

        throw new NotSupportedException("Negation of complex expressions is not supported.");
    }

    private static FilterDescriptor VisitBoolMember(MemberExpression member, bool expectedValue)
    {
        var propertyPath = ExpressionHelper.GetPropertyPath(member);
        return new FilterDescriptor { PropertyName = propertyPath, Operator = FilterOperator.Equals, Value = expectedValue };
    }

    private static (string PropertyPath, object? Value) ExtractPropertyAndValue(Expression left, Expression right)
    {
        if (IsMemberChain(left) && !IsMemberChain(right))
            return (ExpressionHelper.GetPropertyPath(left), ResolveValue(right));

        if (IsMemberChain(right) && !IsMemberChain(left))
            return (ExpressionHelper.GetPropertyPath(right), ResolveValue(left));

        if (IsMemberChain(left))
            return (ExpressionHelper.GetPropertyPath(left), ResolveValue(right));

        throw new NotSupportedException("Cannot determine property and value from comparison expression.");
    }

    private static bool IsMemberChain(Expression expression)
    {
        var current = expression;

        if (current is UnaryExpression unary)
            current = unary.Operand;

        while (current is MemberExpression member)
        {
            if (member.Expression is ParameterExpression)
                return true;

            current = member.Expression;
        }

        return false;
    }

    private static object? ResolveValue(Expression expression)
    {
        if (expression is ConstantExpression constant)
            return constant.Value;

        var lambda = Expression.Lambda(expression);
        return lambda.Compile().DynamicInvoke();
    }
}
