using System.Collections.Generic;
using System.Linq.Expressions;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Configuration for a single column in a BaseList.
/// </summary>
public class PropertyColumnConfig<TModel>
{
    public required Expression<Func<TModel, object?>> Property { get; set; }

    public string PropertyName { get; internal set; } = string.Empty;

    public string? Title { get; set; }

    /// <summary>
    /// Optional header tooltip. When null, resolved from the model localizer key
    /// <c>&lt;PropertyName&gt;_Tooltip</c>; no tooltip is shown when that key is absent.
    /// </summary>
    public string? Tooltip { get; set; }

    public bool Sortable { get; set; } = true;

    public bool Visible { get; set; } = true;

    public RenderFragment<TModel>? Template { get; set; }

    public string? Format { get; set; }

    public int Order { get; set; }

    public string? Width { get; set; }

    /// <summary>
    /// View-level access rules that further restrict the effective rights for this column.
    /// Combined with class/property-level attributes via intersection.
    /// </summary>
    public List<CrudAccessRule> AccessRules { get; set; } = new();

    internal string ResolvePropertyName()
    {
        if (!string.IsNullOrEmpty(PropertyName))
            return PropertyName;

        PropertyName = ExpressionHelper.GetPropertyName(Property);
        return PropertyName;
    }

    private Func<TModel, object?>? CompiledProperty;

    internal object? GetValue(TModel model)
    {
        CompiledProperty ??= Property.Compile();
        return CompiledProperty(model);
    }
}
