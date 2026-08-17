using System.Linq.Expressions;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for a <see cref="FilterConfiguration{TModel}"/>.
/// </summary>
public class FilterConfigBuilder<TModel> where TModel : class
{
    private readonly FilterConfiguration<TModel> configuration = new();

    /// <summary>Disables filtering for the list entirely (hides the filter button).</summary>
    public FilterConfigBuilder<TModel> Disable()
    {
        configuration.Enabled = false;
        return this;
    }

    /// <summary>Sets the selection mode explicitly.</summary>
    public FilterConfigBuilder<TModel> Mode(FilterFieldSelectionMode mode)
    {
        configuration.Mode = mode;
        return this;
    }

    /// <summary>Adds a per-field override (label/operators) without changing the selection mode.</summary>
    public FilterConfigBuilder<TModel> Field(
        Expression<Func<TModel, object?>> property,
        Action<FilterFieldBuilder<TModel>>? configure = null)
    {
        AddField(property, configure);
        return this;
    }

    /// <summary>Switches to opt-in: only the included fields are filterable.</summary>
    public FilterConfigBuilder<TModel> Include(
        Expression<Func<TModel, object?>> property,
        Action<FilterFieldBuilder<TModel>>? configure = null)
    {
        SetMode(FilterFieldSelectionMode.Include);
        AddField(property, configure);
        return this;
    }

    /// <summary>Switches to opt-out: every scalar field except the excluded ones is filterable.</summary>
    public FilterConfigBuilder<TModel> Exclude(Expression<Func<TModel, object?>> property)
    {
        SetMode(FilterFieldSelectionMode.Exclude);
        AddField(property, null);
        return this;
    }

    public FilterConfiguration<TModel> Build() => configuration;

    private void SetMode(FilterFieldSelectionMode mode)
    {
        if (configuration.Mode != FilterFieldSelectionMode.Auto && configuration.Mode != mode)
            throw new InvalidOperationException("Filter configuration cannot mix Include and Exclude modes.");

        configuration.Mode = mode;
    }

    private void AddField(Expression<Func<TModel, object?>> property, Action<FilterFieldBuilder<TModel>>? configure)
    {
        var field = new FilterFieldConfig<TModel> { Property = property };
        field.ResolvePropertyName();

        if (configure is not null)
        {
            var builder = new FilterFieldBuilder<TModel>(field);
            configure(builder);
        }

        configuration.Fields.Add(field);
    }
}

/// <summary>
/// Fluent builder for configuring a single filter field.
/// </summary>
public class FilterFieldBuilder<TModel>(FilterFieldConfig<TModel> config)
{
    private readonly FilterFieldConfig<TModel> config = config;

    public FilterFieldBuilder<TModel> Label(string label)
    {
        config.Label = label;
        return this;
    }

    public FilterFieldBuilder<TModel> Operators(params FilterOperator[] operators)
    {
        config.AllowedOperators = [.. operators];
        return this;
    }
}
