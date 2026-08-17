using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Built configuration for a BaseCard, produced by BaseCardBuilder or collected from child components.
/// </summary>
public class BaseCardConfiguration<TModel> where TModel : class
{
    public List<PropertyFieldConfig<TModel>> Fields { get; set; } = [];

    public List<BaseListPartConfiguration<TModel>> ListParts { get; set; } = [];

    public List<CrudActionGroup<TModel>> ActionGroups { get; set; } = [];

    /// <summary>
    /// Properties whose values form the card header title (in ascending order),
    /// replacing the primary key. Empty falls back to <see cref="Attributes.DisplayKeyAttribute"/>
    /// on the entity, then to the primary key.
    /// </summary>
    public List<DisplayKeyFieldConfig<TModel>> DisplayKeys { get; set; } = [];

    /// <summary>
    /// Separator placed between multiple display-key values in the header title.
    /// Null falls back to <see cref="Display.DisplayKeyResolver.DefaultSeparator"/>.
    /// </summary>
    public string? DisplayKeySeparator { get; set; }
}
