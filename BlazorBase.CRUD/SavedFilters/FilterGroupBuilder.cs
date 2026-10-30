using System.Linq.Expressions;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Fluent builder for the conditions of a preset or default filter, typed against the list's model.
/// </summary>
public sealed class FilterGroupBuilder<TModel> where TModel : class
{
    private readonly FilterGroupModel group = new();

    /// <summary>
    /// Combines the conditions with OR instead of the default AND.
    /// </summary>
    public FilterGroupBuilder<TModel> Or()
    {
        group.Logic = FilterLogic.Or;
        return this;
    }

    /// <summary>
    /// Adds a condition on <paramref name="property"/>. Value-less operators such as <see cref="FilterOperator.IsNull"/>
    /// ignore <paramref name="value"/>.
    /// </summary>
    public FilterGroupBuilder<TModel> Where(Expression<Func<TModel, object?>> property, FilterOperator filterOperator, object? value = null)
    {
        group.Conditions.Add(new FilterConditionModel
        {
            PropertyName = ExpressionHelper.GetPropertyName(property),
            Operator = filterOperator,
            Value = FilterModelConverter.IsValueLess(filterOperator) ? null : FilterValueFormatter.Format(value)
        });
        return this;
    }

    /// <summary>
    /// Adds a condition that the property equals <paramref name="value"/>.
    /// </summary>
    public FilterGroupBuilder<TModel> Is(Expression<Func<TModel, object?>> property, object? value) =>
        Where(property, FilterOperator.Equals, value);

    /// <summary>
    /// Adds a nested group, for filters such as "A and (B or C)".
    /// </summary>
    public FilterGroupBuilder<TModel> Group(Action<FilterGroupBuilder<TModel>> configure)
    {
        var nested = new FilterGroupBuilder<TModel>();
        configure(nested);
        group.Groups.Add(nested.Build());
        return this;
    }

    public FilterGroupModel Build() => group.Clone();
}
