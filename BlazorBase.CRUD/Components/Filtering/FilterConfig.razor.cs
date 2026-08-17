using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components.Filtering;

/// <summary>
/// Declarative filter configuration for a <see cref="BaseList{TModel}"/>. Place inside the list and
/// add <see cref="FilterField{TModel}"/> children to opt in/out of filterable fields.
/// </summary>
public partial class FilterConfig<TModel> : IFilterFieldCollector<TModel> where TModel : class
{
    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public FilterFieldSelectionMode Mode { get; set; } = FilterFieldSelectionMode.Auto;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private IColumnCollector<TModel>? ListCollector { get; set; }

    private readonly List<FilterFieldConfig<TModel>> fields = [];

    void IFilterFieldCollector<TModel>.AddField(FilterFieldConfig<TModel> field) => fields.Add(field);

    protected override void OnInitialized()
        => (ListCollector as IFilterConfigCollector<TModel>)?.RegisterFilterConfig(this);

    internal FilterConfiguration<TModel> BuildConfiguration() => new()
    {
        Enabled = Enabled,
        Mode = Mode,
        Fields = [.. fields]
    };
}
