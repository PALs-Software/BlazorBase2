using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Converts the editable <see cref="FilterGroupModel"/> tree into a serializable
/// <see cref="FilterDescriptor"/> tree, dropping incomplete conditions.
/// </summary>
public static class FilterModelConverter
{
    public static FilterDescriptor? ToDescriptor(FilterGroupModel group)
    {
        var children = new List<FilterDescriptor>();

        foreach (var condition in group.Conditions)
        {
            var descriptor = ToConditionDescriptor(condition);
            if (descriptor is not null)
                children.Add(descriptor);
        }

        foreach (var childGroup in group.Groups)
        {
            var descriptor = ToDescriptor(childGroup);
            if (descriptor is not null)
                children.Add(descriptor);
        }

        if (children.Count == 0)
            return null;

        return new FilterDescriptor
        {
            Logic = group.Logic,
            Filters = children
        };
    }

    public static int CountConditions(FilterGroupModel group)
    {
        var count = group.Conditions.Count(IsComplete);

        foreach (var childGroup in group.Groups)
            count += CountConditions(childGroup);

        return count;
    }

    public static bool IsValueLess(FilterOperator op) =>
        op is FilterOperator.IsNull or FilterOperator.IsNotNull;

    private static bool IsComplete(FilterConditionModel condition)
    {
        if (string.IsNullOrEmpty(condition.PropertyName))
            return false;

        if (IsValueLess(condition.Operator))
            return true;

        return !string.IsNullOrWhiteSpace(condition.Value);
    }

    private static FilterDescriptor? ToConditionDescriptor(FilterConditionModel condition)
    {
        if (!IsComplete(condition))
            return null;

        return new FilterDescriptor
        {
            PropertyName = condition.PropertyName,
            Operator = condition.Operator,
            Value = IsValueLess(condition.Operator) ? null : condition.Value
        };
    }
}
