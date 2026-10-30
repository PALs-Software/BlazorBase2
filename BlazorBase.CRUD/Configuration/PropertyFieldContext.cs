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

    /// <summary>The localized label of the field, so a template can pass it on to a label-capable control.</summary>
    public string? Label { get; init; }

    /// <summary>True when the field is required (configured or declared with <c>[Required]</c>).</summary>
    public bool Required { get; init; }

    public EventCallback<object?> ValueChanged { get; init; }
}
