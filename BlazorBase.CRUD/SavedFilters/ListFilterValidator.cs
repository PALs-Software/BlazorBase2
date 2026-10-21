using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Limits applied to everything a user can store, so a client cannot persist unbounded data.
/// </summary>
public static class ListFilterValidator
{
    public const int MaxNameLength = 100;

    public const int MaxListKeyLength = 256;

    public const int MaxFiltersPerList = 100;

    public const int MaxDepth = 6;

    public const int MaxConditions = 100;

    public const int MaxValueLength = 1000;

    public const int MaxPropertyNameLength = 200;

    /// <summary>
    /// Trims the name and rejects an empty or over-long one.
    /// </summary>
    public static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();

        if (String.IsNullOrEmpty(trimmed))
            throw new ListFilterValidationException("The filter name must not be empty.");

        if (trimmed.Length > MaxNameLength)
            throw new ListFilterValidationException($"The filter name must not be longer than {MaxNameLength} characters.");

        return trimmed;
    }

    public static string ValidateListKey(string? listKey)
    {
        if (String.IsNullOrWhiteSpace(listKey))
            throw new ListFilterValidationException("The list key must not be empty.");

        if (listKey.Length > MaxListKeyLength)
            throw new ListFilterValidationException($"The list key must not be longer than {MaxListKeyLength} characters.");

        return listKey;
    }

    /// <summary>
    /// Rejects a filter tree that is missing, too deep, too large or carries undefined operators.
    /// </summary>
    public static void ValidateFilter(FilterGroupModel? filter)
    {
        if (filter is null)
            throw new ListFilterValidationException("The filter must not be empty.");

        var conditions = 0;
        ValidateGroup(filter, 1, ref conditions);
    }

    private static void ValidateGroup(FilterGroupModel group, int depth, ref int conditions)
    {
        if (depth > MaxDepth)
            throw new ListFilterValidationException($"The filter must not be nested deeper than {MaxDepth} levels.");

        if (!Enum.IsDefined(group.Logic))
            throw new ListFilterValidationException("The filter contains an unknown logic.");

        foreach (var condition in group.Conditions)
        {
            conditions++;

            if (conditions > MaxConditions)
                throw new ListFilterValidationException($"The filter must not have more than {MaxConditions} conditions.");

            ValidateCondition(condition);
        }

        foreach (var child in group.Groups)
            ValidateGroup(child, depth + 1, ref conditions);
    }

    private static void ValidateCondition(FilterConditionModel condition)
    {
        if (!Enum.IsDefined(condition.Operator))
            throw new ListFilterValidationException("The filter contains an unknown operator.");

        if (condition.PropertyName?.Length > MaxPropertyNameLength)
            throw new ListFilterValidationException("The filter contains a property name that is too long.");

        if (condition.Value?.Length > MaxValueLength)
            throw new ListFilterValidationException($"A filter value must not be longer than {MaxValueLength} characters.");
    }
}
