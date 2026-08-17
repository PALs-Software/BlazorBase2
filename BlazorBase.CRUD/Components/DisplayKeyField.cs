using System;
using System.Linq.Expressions;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Marker child component for declaring a display-key property inside a BaseCard.
/// Collected by the parent card to build the header title and is decoupled from
/// field rendering — the referenced property need not be an editable field.
/// </summary>
public class DisplayKeyField<TModel> : ComponentBase
{
    [Parameter, EditorRequired]
    public Expression<Func<TModel, object?>> Property { get; set; } = default!;

    /// <summary>Position within the combined title; lower values appear first.</summary>
    [Parameter]
    public int Order { get; set; }

    [CascadingParameter]
    private IDisplayKeyCollector<TModel>? DisplayKeyCollector { get; set; }

    protected override void OnInitialized()
    {
        if (DisplayKeyCollector is null)
            return;

        var config = new DisplayKeyFieldConfig<TModel> { Property = Property, Order = Order };
        config.ResolvePropertyName();
        DisplayKeyCollector.AddDisplayKey(config);
    }
}

internal interface IDisplayKeyCollector<TModel>
{
    void AddDisplayKey(DisplayKeyFieldConfig<TModel> displayKey);
}
