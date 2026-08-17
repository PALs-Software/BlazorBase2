using System.Reflection;

namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Decision inputs handed to <see cref="IBaseCustomPropertyInput.CanHandle"/> and
/// <see cref="IBaseCustomPropertyDisplay.CanHandle"/> so a registered component can decide whether
/// it wants to render a given property by model type, property type, attribute or name.
/// </summary>
/// <param name="ModelType">The entity/model type the property belongs to.</param>
/// <param name="Property">Reflection info for the property being rendered.</param>
/// <param name="PropertyType">The property type with any <see cref="System.Nullable{T}"/> wrapper unwrapped.</param>
/// <param name="IsEditing">True for an editable card field; always false for list cells.</param>
public sealed record CustomPropertyContext(
    Type ModelType,
    PropertyInfo Property,
    Type PropertyType,
    bool IsEditing);
