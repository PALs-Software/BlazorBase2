using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.FluentUI.AspNetCore.Components;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Configuration;

/// <summary>
/// Fluent builder for configuring a single CRUD action within a group.
/// </summary>
public class CrudActionBuilder<TModel> where TModel : class
{
    private readonly CrudAction<TModel> config = new();

    public CrudActionBuilder<TModel> Caption(string caption) { config.Caption = caption; return this; }
    public CrudActionBuilder<TModel> ToolTip(string toolTip) { config.ToolTip = toolTip; return this; }
    public CrudActionBuilder<TModel> Icon(Icon icon) { config.Icon = icon; return this; }
    public CrudActionBuilder<TModel> Appearance(Appearance appearance) { config.Appearance = appearance; return this; }
    public CrudActionBuilder<TModel> ShowIn(CrudActionContext contexts) { config.Contexts = contexts; return this; }
    public CrudActionBuilder<TModel> Order(int order) { config.Order = order; return this; }
    public CrudActionBuilder<TModel> Visible(Func<CrudActionVisibilityArgs<TModel>, Task<bool>> predicate) { config.Visible = predicate; return this; }
    public CrudActionBuilder<TModel> VisibleForRoles(Func<ClaimsPrincipal, bool> predicate) { config.VisibleForRoles = predicate; return this; }
    public CrudActionBuilder<TModel> OnExecute(Func<CrudActionEventArgs<TModel>, Task> action) { config.Action = action; return this; }
    public CrudActionBuilder<TModel> BulkAction(bool isBulk = true) { config.IsBulkAction = isBulk; return this; }

    public CrudActionBuilder<TModel> DynamicComponent<TComponent>(
        Dictionary<string, object?>? parameters = null,
        Func<CrudActionEventArgs<TModel>, Task>? onClosed = null) where TComponent : Microsoft.AspNetCore.Components.ComponentBase
    {
        config.DynamicComponent = new CrudActionComponentConfig<TModel>
        {
            ComponentType = typeof(TComponent),
            Parameters = parameters,
            OnComponentClosed = onClosed
        };
        return this;
    }

    public CrudActionBuilder<TModel> DynamicComponent(
        Type componentType,
        Dictionary<string, object?>? parameters = null,
        Func<CrudActionEventArgs<TModel>, Task>? onClosed = null)
    {
        config.DynamicComponent = new CrudActionComponentConfig<TModel>
        {
            ComponentType = componentType,
            Parameters = parameters,
            OnComponentClosed = onClosed
        };
        return this;
    }

    internal CrudAction<TModel> Build() => config;
}
