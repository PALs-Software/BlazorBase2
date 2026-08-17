using System.Security.Claims;
using BlazorBase.CRUD.Events;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Models;

/// <summary>
/// Groups related CRUD actions together. Rendered as a dropdown menu button in toolbars
/// or as a labeled section in context menus.
/// </summary>
public class CrudActionGroup<TModel> where TModel : class
{
    public required string Name { get; set; }

    public string? Caption { get; set; }

    public Icon? Icon { get; set; }

    public CrudActionContext Contexts { get; set; } = CrudActionContext.All;

    public int Order { get; set; }

    public Func<CrudActionVisibilityArgs<TModel>, Task<bool>>? Visible { get; set; }

    public Func<ClaimsPrincipal, bool>? VisibleForRoles { get; set; }

    public List<CrudAction<TModel>> Actions { get; set; } = [];
}
