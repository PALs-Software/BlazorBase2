using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Context passed to custom editor and display templates in PropertyFieldConfig.
/// </summary>
public class PropertyFieldContext<TModel>
{
    public required TModel Model { get; init; }

    public object? Value { get; init; }

    public bool IsEditing { get; init; }

    public EventCallback<object?> ValueChanged { get; init; }
}
