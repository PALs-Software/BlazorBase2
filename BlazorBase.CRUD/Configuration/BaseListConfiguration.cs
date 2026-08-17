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
}
