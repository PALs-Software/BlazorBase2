using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Maps a <see cref="FilterFieldKind"/> to the operators that make sense for it.
/// </summary>
public static class FilterOperatorCatalog
{
    public static IReadOnlyList<FilterOperator> GetDefaultOperators(FilterFieldKind kind, bool isNullable)
    {
        var operators = new List<FilterOperator>();

        switch (kind)
        {
            case FilterFieldKind.Text:
                operators.AddRange([
                    FilterOperator.Contains,
                    FilterOperator.StartsWith,
                    FilterOperator.EndsWith,
                    FilterOperator.Equals,
                    FilterOperator.NotEquals
                ]);
                break;

            case FilterFieldKind.Number:
            case FilterFieldKind.Date:
                operators.AddRange([
                    FilterOperator.Equals,
                    FilterOperator.NotEquals,
                    FilterOperator.GreaterThan,
                    FilterOperator.GreaterThanOrEqual,
                    FilterOperator.LessThan,
                    FilterOperator.LessThanOrEqual
                ]);
                break;

            case FilterFieldKind.Boolean:
                operators.Add(FilterOperator.Equals);
                break;

            case FilterFieldKind.Enum:
            case FilterFieldKind.Guid:
                operators.AddRange([FilterOperator.Equals, FilterOperator.NotEquals]);
                break;
        }

        if (isNullable)
        {
            operators.Add(FilterOperator.IsNull);
            operators.Add(FilterOperator.IsNotNull);
        }

        return operators;
    }
}
