using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Filtering;

/// <summary>
/// Turns the text typed into a list's quick search into one <see cref="FilterDescriptor"/>: a "contains" over each searched
/// property, joined with OR, so a row matches when any of the properties holds the text.
/// </summary>
/// <remarks>
/// The descriptor is an ordinary filter, so it travels through every data provider (EF Core, HTTP, in-memory) like a
/// column filter does, and the list combines it with its other filters by AND. Whether "contains" ignores case is up to
/// the provider: the in-memory provider ignores it, a database follows the collation of the column.
/// </remarks>
public static class QuickSearchFilterBuilder
{
    /// <summary>
    /// Builds the filter for <paramref name="searchText"/>, or null when there is nothing to search for (blank text, or
    /// no property to search in).
    /// </summary>
    /// <param name="propertyPaths">The properties to search, as returned by <see cref="QuickSearchPropertyResolver.Resolve"/>.</param>
    /// <param name="searchText">The text the user typed; leading and trailing white space is ignored.</param>
    public static FilterDescriptor? Build(IReadOnlyList<string> propertyPaths, string? searchText)
    {
        ArgumentNullException.ThrowIfNull(propertyPaths);

        var text = searchText?.Trim();

        if (string.IsNullOrEmpty(text) || propertyPaths.Count == 0)
            return null;

        var conditions = propertyPaths
            .Select(propertyPath => new FilterDescriptor
            {
                PropertyName = propertyPath,
                Operator = FilterOperator.Contains,
                Value = text
            })
            .ToList();

        if (conditions.Count == 1)
            return conditions[0];

        return new FilterDescriptor
        {
            Logic = FilterLogic.Or,
            Filters = conditions
        };
    }
}
