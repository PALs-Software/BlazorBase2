namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Declares whether and which fields a <see cref="Components.BaseList{TModel}"/> exposes to the
/// advanced filter panel. When nothing is configured every scalar entity field is filterable.
/// </summary>
public class FilterConfiguration<TModel> where TModel : class
{
    public bool Enabled { get; set; } = true;

    public FilterFieldSelectionMode Mode { get; set; } = FilterFieldSelectionMode.Auto;

    public List<FilterFieldConfig<TModel>> Fields { get; set; } = [];
}
