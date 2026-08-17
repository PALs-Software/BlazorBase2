using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Contract for a DI-registered, reusable custom list-cell display component. Implemented by a
/// consumer Razor component and registered via <c>AddBlazorBaseCustomDisplay&lt;TComponent&gt;()</c>.
/// The generic list resolves the first registered component whose <see cref="CanHandle"/> returns
/// true for a column's property and renders it instead of the default cell text.
/// </summary>
public interface IBaseCustomPropertyDisplay : IComponent
{
    /// <summary>
    /// Synchronous, pure, cheap decision whether this component renders the given property's cell.
    /// <see cref="CustomPropertyContext.IsEditing"/> is always false for list cells.
    /// </summary>
    bool CanHandle(CustomPropertyContext context);

    /// <summary>The bound model instance for the row.</summary>
    object Model { get; set; }

    /// <summary>Reflection info for the property being rendered.</summary>
    PropertyInfo Property { get; set; }

    /// <summary>The current property value.</summary>
    object? Value { get; set; }

    /// <summary>Resolved property localizer, or null when none is available.</summary>
    IStringLocalizer? Localizer { get; set; }
}
