using System.Security.Claims;
using BlazorBase.CRUD.Events;
using Microsoft.FluentUI.AspNetCore.Components;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Models;

/// <summary>
/// Defines a single custom action that can be shown in list toolbars, context menus, cards, or list parts.
/// </summary>
public class CrudAction<TModel> where TModel : class
{
    public string? Caption { get; set; }

    public string? ToolTip { get; set; }

    public Icon? Icon { get; set; }

    public Appearance Appearance { get; set; } = Appearance.Stealth;

    public CrudActionContext Contexts { get; set; } = CrudActionContext.All;

    public int Order { get; set; }

    public Func<CrudActionVisibilityArgs<TModel>, Task<bool>>? Visible { get; set; }

    public Func<ClaimsPrincipal, bool>? VisibleForRoles { get; set; }

    public Func<CrudActionEventArgs<TModel>, Task>? Action { get; set; }

    public bool IsBulkAction { get; set; }

    public CrudActionComponentConfig<TModel>? DynamicComponent { get; set; }
}
