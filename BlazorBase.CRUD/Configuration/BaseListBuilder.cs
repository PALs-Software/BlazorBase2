using System.Linq.Expressions;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for constructing BaseListConfiguration via code-behind.
/// </summary>
public class BaseListBuilder<TModel> where TModel : class
{
    private readonly List<PropertyColumnConfig<TModel>> columns = [];
    private readonly List<CrudActionGroup<TModel>> actionGroups = [];
    private FilterConfiguration<TModel>? filterConfiguration;
    private int orderCounter;
    private int actionGroupOrderCounter;

    public BaseListBuilder<TModel> Column(
        Expression<Func<TModel, object?>> property,
        Action<ColumnBuilder<TModel>>? configure = null)
    {
        var config = new PropertyColumnConfig<TModel>
        {
            Property = property,
            Order = orderCounter++
        };
        config.ResolvePropertyName();

        if (configure is not null)
        {
            var builder = new ColumnBuilder<TModel>(config);
            configure(builder);
        }

        columns.Add(config);
        return this;
    }

    public BaseListBuilder<TModel> ActionGroup(
        string name,
        Action<CrudActionGroupBuilder<TModel>> configure)
    {
        var builder = new CrudActionGroupBuilder<TModel>(name);
        builder.Order(actionGroupOrderCounter++);
        configure(builder);
        actionGroups.Add(builder.Build());
        return this;
    }

    public BaseListBuilder<TModel> Filtering(Action<FilterConfigBuilder<TModel>> configure)
    {
        var builder = new FilterConfigBuilder<TModel>();
        configure(builder);
        filterConfiguration = builder.Build();
        return this;
    }

    public BaseListConfiguration<TModel> Build()
    {
        return new BaseListConfiguration<TModel>
        {
            Columns = [.. columns],
            ActionGroups = [.. actionGroups],
            Filter = filterConfiguration
        };
    }
}

/// <summary>
/// Fluent builder for configuring a single column.
/// </summary>
public class ColumnBuilder<TModel>(PropertyColumnConfig<TModel> config)
{
    public ColumnBuilder<TModel> Title(string title) { config.Title = title; return this; }
    public ColumnBuilder<TModel> Sortable(bool sortable = true) { config.Sortable = sortable; return this; }
    public ColumnBuilder<TModel> Visible(bool visible = true) { config.Visible = visible; return this; }
    public ColumnBuilder<TModel> Format(string format) { config.Format = format; return this; }
    public ColumnBuilder<TModel> Width(string width) { config.Width = width; return this; }
    /// <summary>
    /// Adds a view-level access rule. May be called multiple times to grant rights to
    /// additional roles. Rights string accepts R/I/M/D characters; view rules can only
    /// further restrict what the class/property level allows.
    /// </summary>
    public ColumnBuilder<TModel> Access(string roles, string rights)
    {
        config.AccessRules.Add(CrudAccessResolver.ParseRule(roles, rights, allowDelete: true));
        return this;
    }
}
