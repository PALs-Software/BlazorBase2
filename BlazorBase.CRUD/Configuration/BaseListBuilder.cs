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
    private readonly List<SortDescriptor> defaultSorts = [];
    private readonly SavedFilters.ListFilterDefaultsBuilder<TModel> filterDefaults = new();
    private FilterConfiguration<TModel>? filterConfiguration;
    private MobileCardConfiguration<TModel>? mobileCard;
    private int? mobileBreakpoint;
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

    /// <summary>
    /// Adds a default sort: the order the list shows until the user sorts it. Call repeatedly for a
    /// tie-breaker.
    /// </summary>
    public BaseListBuilder<TModel> DefaultSort(Expression<Func<TModel, object?>> property, bool descending = false)
    {
        defaultSorts.Add(new SortDescriptor
        {
            PropertyName = ExpressionHelper.GetPropertyName(property),
            Direction = descending ? SortDirection.Descending : SortDirection.Ascending
        });
        return this;
    }

    /// <summary>
    /// Offers a named filter ("preset") to every user of the list. See
    /// <see cref="SavedFilters.ListFilterDefaultsBuilder{TModel}.Preset"/>.
    /// </summary>
    public BaseListBuilder<TModel> FilterPreset(string id, string name, Action<SavedFilters.FilterGroupBuilder<TModel>> configure, bool isDefault = false)
    {
        filterDefaults.Preset(id, name, configure, isDefault);
        return this;
    }

    /// <summary>
    /// Declares the filter the list applies on first load unless the user saved a default of their own. See
    /// <see cref="SavedFilters.ListFilterDefaultsBuilder{TModel}.DefaultFilter"/>.
    /// </summary>
    public BaseListBuilder<TModel> DefaultFilter(Action<SavedFilters.FilterGroupBuilder<TModel>> configure)
    {
        filterDefaults.DefaultFilter(configure);
        return this;
    }

    /// <summary>
    /// Sets the key under which users' saved filters for this list are stored (default: <c>&lt;model full name&gt;@&lt;route&gt;</c>;
    /// set it for lists in dialogs or list parts, which share the page route).
    /// </summary>
    public BaseListBuilder<TModel> FilterListKey(string listKey)
    {
        filterDefaults.ListKey(listKey);
        return this;
    }

    /// <summary>
    /// Declares the tile the list shows instead of its table on a narrow screen: which properties it shows and
    /// the part each plays. Without it the list uses the roles set on its columns, then a choice by data type.
    /// </summary>
    public BaseListBuilder<TModel> MobileCard(Action<MobileCardBuilder<TModel>> configure)
    {
        var builder = new MobileCardBuilder<TModel>();
        configure(builder);
        mobileCard = builder.Build();
        return this;
    }

    /// <summary>
    /// The container width in pixels below which the list shows tiles instead of its table (700 by default);
    /// 0 keeps the table at every width.
    /// </summary>
    public BaseListBuilder<TModel> MobileBreakpoint(int pixels)
    {
        mobileBreakpoint = pixels;
        return this;
    }

    public BaseListConfiguration<TModel> Build()
    {
        return new BaseListConfiguration<TModel>
        {
            Columns = [.. columns],
            ActionGroups = [.. actionGroups],
            Filter = filterConfiguration,
            DefaultSorts = [.. defaultSorts],
            FilterDefaults = filterDefaults.HasContent ? filterDefaults.Build() : null,
            MobileCard = mobileCard,
            MobileBreakpoint = mobileBreakpoint
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
    /// Sets the part the column plays on the tile shown on a narrow screen, and optionally its position among the
    /// columns of the same role.
    /// </summary>
    public ColumnBuilder<TModel> Mobile(MobileRole role, int? priority = null)
    {
        config.MobileRole = role;
        config.MobilePriority = priority;
        return this;
    }
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
