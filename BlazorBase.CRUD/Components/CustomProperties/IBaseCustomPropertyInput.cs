using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Contract for a DI-registered, reusable custom card input component. Implemented by a consumer
/// Razor component and registered via <c>AddBlazorBaseCustomInput&lt;TComponent&gt;()</c>. The
/// generic card resolves the first registered component whose <see cref="CanHandle"/> returns true
/// for a property and renders it instead of the built-in type-based input.
/// </summary>
public interface IBaseCustomPropertyInput : IComponent
{
    /// <summary>
    /// Synchronous, pure, cheap decision whether this component renders the given property.
    /// Decide by <see cref="CustomPropertyContext.PropertyType"/>, a custom attribute on
    /// <see cref="CustomPropertyContext.Property"/>, or the property name.
    /// </summary>
    bool CanHandle(CustomPropertyContext context);

    /// <summary>The bound model instance.</summary>
    object Model { get; set; }

    /// <summary>Reflection info for the property being rendered.</summary>
    PropertyInfo Property { get; set; }

    /// <summary>The current property value.</summary>
    object? Value { get; set; }

    /// <summary>Invoked by the component to push a new value back onto the model.</summary>
    EventCallback<object?> ValueChanged { get; set; }

    /// <summary>True when the card is in edit mode.</summary>
    bool IsEditing { get; set; }

    /// <summary>True when the property must be rendered read-only.</summary>
    bool ReadOnly { get; set; }

    /// <summary>Resolved property localizer, or null when none is available.</summary>
    IStringLocalizer? Localizer { get; set; }
}
