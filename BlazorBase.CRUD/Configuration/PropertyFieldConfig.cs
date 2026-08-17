using System.Collections.Generic;
using System.Linq.Expressions;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Configuration for a single field in a BaseCard or BaseListPart.
/// </summary>
public class PropertyFieldConfig<TModel>
{
    public required Expression<Func<TModel, object?>> Property { get; set; }

    public string PropertyName { get; internal set; } = string.Empty;

    public string? Label { get; set; }

    /// <summary>
    /// Optional field tooltip. When null, resolved from the model localizer key
    /// <c>&lt;PropertyName&gt;_Tooltip</c>; no tooltip is shown when that key is absent.
    /// </summary>
    public string? Tooltip { get; set; }

    public bool Editable { get; set; } = true;

    public bool Required { get; set; }

    /// <summary>
    /// Optional input placeholder. When null, resolved from the model localizer key
    /// <c>&lt;PropertyName&gt;_Placeholder</c>; no placeholder is shown when that key is absent.
    /// </summary>
    public string? Placeholder { get; set; }

    public string? Group { get; set; }

    public int Order { get; set; }

    public int ColSpan { get; set; } = 1;

    public int? Lines { get; set; }

    public RenderFragment<PropertyFieldContext<TModel>>? EditorTemplate { get; set; }

    public RenderFragment<PropertyFieldContext<TModel>>? DisplayTemplate { get; set; }

    /// <summary>
    /// View-level access rules that further restrict the effective rights for this field.
    /// Combined with class/property-level attributes via intersection. Use the fluent
    /// builder's <c>Access(roles, rights)</c> method or markup parameter to populate.
    /// </summary>
    public List<CrudAccessRule> AccessRules { get; set; } = new();

    public int LookupThreshold { get; set; } = 20;

    /// <summary>
    /// When true, this field's property also contributes to the card header title
    /// (a display key), ordered by <see cref="DisplayKeyOrder"/>.
    /// </summary>
    public bool IsDisplayKey { get; set; }

    /// <summary>Position of this field within the display-key title when <see cref="IsDisplayKey"/> is set.</summary>
    public int DisplayKeyOrder { get; set; }

    public string? DisplayPropertyName { get; set; }

    public object? LookupListConfiguration { get; set; }

    internal string ResolvePropertyName()
    {
        if (!string.IsNullOrEmpty(PropertyName))
            return PropertyName;

        PropertyName = ExpressionHelper.GetPropertyName(Property);
        return PropertyName;
    }
}
