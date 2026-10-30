using BlazorBase.CRUD.Filtering;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Cleans a stored or declared filter before it is applied: drops conditions that are incomplete or that name a field
/// the list no longer offers (renamed property, field hidden from this user), so an outdated filter never breaks the
/// list's query.
/// </summary>
public static class FilterModelPruner
{
    public static FilterGroupModel Prune(FilterGroupModel filter, IReadOnlyList<FilterFieldMetadata> fields)
    {
        var pruned = new FilterGroupModel { Logic = filter.Logic };

        foreach (var condition in filter.Conditions)
        {
            if (IsUsable(condition, fields))
                pruned.Conditions.Add(condition.Clone());
        }

        foreach (var child in filter.Groups)
        {
            var prunedChild = Prune(child, fields);

            if (FilterModelConverter.CountConditions(prunedChild) > 0)
                pruned.Groups.Add(prunedChild);
        }

        return pruned;
    }

    private static bool IsUsable(FilterConditionModel condition, IReadOnlyList<FilterFieldMetadata> fields)
    {
        var field = fields.FirstOrDefault(candidate => candidate.PropertyName == condition.PropertyName);

        if (field is null || !field.Operators.Contains(condition.Operator))
            return false;

        return FilterModelConverter.IsValueLess(condition.Operator) || !String.IsNullOrWhiteSpace(condition.Value);
    }
}
