using System.Linq.Expressions;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Querying;

/// <summary>
/// Fluent builder for <see cref="NavigationFilter"/> with expression-based predicates.
/// </summary>
public class NavigationFilterBuilder<TNav>
{
    private readonly List<FilterDescriptor> Filters = [];
    private readonly List<SortDescriptor> Sorts = [];

    public NavigationFilterBuilder<TNav> Where(Expression<Func<TNav, bool>> predicate)
    {
        Filters.Add(FilterExpressionDecomposer.Decompose(predicate));
        return this;
    }

    public NavigationFilterBuilder<TNav> OrderBy(Expression<Func<TNav, object?>> selector)
    {
        Sorts.Add(new SortDescriptor { PropertyName = ExpressionHelper.GetPropertyName(selector) });
        return this;
    }

    public NavigationFilterBuilder<TNav> OrderByDescending(Expression<Func<TNav, object?>> selector)
    {
        Sorts.Add(new SortDescriptor { PropertyName = ExpressionHelper.GetPropertyName(selector), Direction = SortDirection.Descending });
        return this;
    }

    internal NavigationFilter Build(string navigationName)
    {
        return new NavigationFilter
        {
            NavigationName = navigationName,
            Filters = Filters,
            Sorts = Sorts,
        };
    }
}
