using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Built configuration for a BaseList, produced by BaseListBuilder or collected from child components.
/// </summary>
public class BaseListConfiguration<TModel> where TModel : class
{
    public List<PropertyColumnConfig<TModel>> Columns { get; set; } = [];

    public List<CrudActionGroup<TModel>> ActionGroups { get; set; } = [];

    public FilterConfiguration<TModel>? Filter { get; set; }

    /// <summary>
    /// The presets, default filter and storage key declared in code for the list's saved-filter feature.
    /// </summary>
    public SavedFilters.ListFilterDefaults? FilterDefaults { get; set; }

    /// <summary>
    /// The order the list shows until the user sorts it. Empty falls back to
    /// <see cref="Attributes.DefaultSortAttribute"/> on the entity, then to the primary key.
    /// </summary>
    public List<SortDescriptor> DefaultSorts { get; set; } = [];

    /// <summary>
    /// The tile shown instead of the table on a narrow screen; null chooses it from the column roles and the data
    /// types.
    /// </summary>
    public MobileCardConfiguration<TModel>? MobileCard { get; set; }

    /// <summary>
    /// The container width in pixels below which tiles replace the table; null means the default of 700, 0 keeps
    /// the table at every width.
    /// </summary>
    public int? MobileBreakpoint { get; set; }
}
