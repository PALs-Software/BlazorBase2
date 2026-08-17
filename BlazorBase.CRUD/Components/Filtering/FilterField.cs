using System.Linq.Expressions;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components.Filtering;

/// <summary>
/// Declares a single filterable field inside a <see cref="FilterConfig{TModel}"/>. Its meaning
/// depends on the parent's <see cref="FilterConfig{TModel}.Mode"/> (include-list, exclude-list, or
/// per-field override in Auto mode).
/// </summary>
public class FilterField<TModel> : ComponentBase where TModel : class
{
    [Parameter, EditorRequired]
    public Expression<Func<TModel, object?>> For { get; set; } = default!;

    [Parameter]
    public string? Label { get; set; }

    [Parameter]
    public IEnumerable<FilterOperator>? Operators { get; set; }

    [CascadingParameter]
    private IFilterFieldCollector<TModel>? Collector { get; set; }

    protected override void OnInitialized()
    {
        var config = new FilterFieldConfig<TModel>
        {
            Property = For,
            Label = Label,
            AllowedOperators = Operators?.ToList()
        };
        config.ResolvePropertyName();

        Collector?.AddField(config);
    }
}
