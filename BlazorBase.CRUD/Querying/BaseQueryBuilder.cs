using System.Collections;
using System.Linq.Expressions;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Querying;

/// <summary>
/// Fluent LINQ-like builder for <see cref="BaseQuery"/>.
/// Use <see cref="BaseQueryBuilderExtensions.Query{T}"/> to start a query.
/// </summary>
public class BaseQueryBuilder<T> where T : class
{
    private readonly IBaseDataProvider<T> Provider;
    private readonly List<FilterDescriptor> Filters = [];
    private readonly List<SortDescriptor> Sorts = [];
    private readonly List<string> SelectFields = [];
    private readonly List<NavigationFilter> NavFilters = [];
    private int? SkipCount;
    private int? TakeCount;

    internal BaseQueryBuilder(IBaseDataProvider<T> provider)
    {
        Provider = provider;
    }

    public BaseQueryBuilder<T> Where(Expression<Func<T, bool>> predicate)
    {
        Filters.Add(FilterExpressionDecomposer.Decompose(predicate));
        return this;
    }

    public BaseQueryBuilder<T> OrderBy(Expression<Func<T, object?>> selector)
    {
        Sorts.Add(new SortDescriptor { PropertyName = ExpressionHelper.GetPropertyName(selector) });
        return this;
    }

    public BaseQueryBuilder<T> OrderByDescending(Expression<Func<T, object?>> selector)
    {
        Sorts.Add(new SortDescriptor { PropertyName = ExpressionHelper.GetPropertyName(selector), Direction = SortDirection.Descending });
        return this;
    }

    public BaseQueryBuilder<T> ThenBy(Expression<Func<T, object?>> selector)
    {
        return OrderBy(selector);
    }

    public BaseQueryBuilder<T> ThenByDescending(Expression<Func<T, object?>> selector)
    {
        return OrderByDescending(selector);
    }

    public BaseQueryBuilder<T> Select(params Expression<Func<T, object?>>[] selectors)
    {
        foreach (var selector in selectors)
            SelectFields.Add(ExpressionHelper.GetPropertyName(selector));

        return this;
    }

    public BaseQueryBuilder<T> Include(Expression<Func<T, object?>> navigation)
    {
        SelectFields.Add(ExpressionHelper.GetPropertyName(navigation));
        return this;
    }

    public BaseQueryBuilder<T> Include<TNav>(Expression<Func<T, IEnumerable<TNav>>> navigation, Action<NavigationFilterBuilder<TNav>> configure)
    {
        var navigationName = ExpressionHelper.GetPropertyName(navigation);
        SelectFields.Add(navigationName);

        var builder = new NavigationFilterBuilder<TNav>();
        configure(builder);
        NavFilters.Add(builder.Build(navigationName));

        return this;
    }

    public BaseQueryBuilder<T> Skip(int count)
    {
        SkipCount = count;
        return this;
    }

    public BaseQueryBuilder<T> Take(int count)
    {
        TakeCount = count;
        return this;
    }

    public BaseQuery Build()
    {
        var query = new BaseQuery
        {
            Filters = Filters,
            Sorts = Sorts,
        };

        if (SelectFields.Count > 0)
            query.Select = SelectFields;

        if (NavFilters.Count > 0)
            query.NavigationFilters = NavFilters;

        if (SkipCount.HasValue)
            query.Skip = SkipCount.Value;

        if (TakeCount.HasValue)
            query.Take = TakeCount.Value;

        return query;
    }

    public Task<BaseQueryResult<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        return Provider.GetListAsync(Build(), cancellationToken: cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        TakeCount = 1;
        var result = await Provider.GetListAsync(Build(), cancellationToken: cancellationToken);
        return result.Items.FirstOrDefault();
    }
}

public static class BaseQueryBuilderExtensions
{
    public static BaseQueryBuilder<T> Query<T>(this IBaseDataProvider<T> provider) where T : class
    {
        return new BaseQueryBuilder<T>(provider);
    }
}
