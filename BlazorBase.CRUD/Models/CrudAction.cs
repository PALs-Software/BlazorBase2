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
    private CrudActionContext? contexts;

    public string? Caption { get; set; }

    public string? ToolTip { get; set; }

    public Icon? Icon { get; set; }

    public Appearance Appearance { get; set; } = Appearance.Stealth;

    /// <summary>
    /// The places the action is offered. Without an explicit value the default depends on <see cref="Scope"/>: an
    /// item-bound action (<see cref="CrudActionScope.Single"/> or <see cref="CrudActionScope.Multiple"/>) is offered in
    /// the list toolbar, the row menu, the card and the list part; an action without a scope is offered in the list
    /// toolbar, the card and the list part but never in the row menu. An explicit value always wins.
    /// </summary>
    public CrudActionContext Contexts
    {
        get => contexts ?? DefaultContextsFor(Scope);
        set => contexts = value;
    }

    /// <summary>
    /// Whether <see cref="Contexts"/> was set explicitly instead of coming from the default of the <see cref="Scope"/>.
    /// </summary>
    public bool HasExplicitContexts => contexts.HasValue;

    public int Order { get; set; }

    /// <summary>
    /// Which rows the action works on; see <see cref="CrudActionScope"/>.
    /// </summary>
    public CrudActionScope Scope { get; set; }

    /// <summary>
    /// Decides whether the action is offered at all. In the list toolbar it is evaluated with the current selection
    /// (<see cref="CrudActionVisibilityArgs{TModel}.Item"/> is null while no single row applies), in the row menu with
    /// the clicked row.
    /// </summary>
    public Func<CrudActionVisibilityArgs<TModel>, Task<bool>>? Visible { get; set; }

    public Func<ClaimsPrincipal, bool>? VisibleForRoles { get; set; }

    /// <summary>
    /// Decides per item whether the action can run. A <see cref="CrudActionScope.Multiple"/> action is enabled only
    /// when this returns true for every selected row. Without a predicate every item passes.
    /// </summary>
    public Func<TModel, bool>? Enabled { get; set; }

    /// <summary>
    /// A static text explaining why an item blocks the action. Shown after the generic reason; may be a resource key
    /// the list localizer resolves.
    /// </summary>
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Explains per item why it blocks the action. When it returns text for a blocking item, that text is used instead
    /// of <see cref="DisabledReason"/>.
    /// </summary>
    public Func<TModel, string?>? DisabledReasonProvider { get; set; }

    public Func<CrudActionEventArgs<TModel>, Task>? Action { get; set; }

    /// <summary>
    /// Compatibility alias for <see cref="Scope"/>: true means <see cref="CrudActionScope.Multiple"/>. Setting it to
    /// false only resets a <see cref="CrudActionScope.Multiple"/> scope to <see cref="CrudActionScope.None"/>.
    /// </summary>
    public bool IsBulkAction
    {
        get => Scope == CrudActionScope.Multiple;
        set
        {
            if (value)
                Scope = CrudActionScope.Multiple;
            else if (Scope == CrudActionScope.Multiple)
                Scope = CrudActionScope.None;
        }
    }

    public CrudActionComponentConfig<TModel>? DynamicComponent { get; set; }

    /// <summary>
    /// The default <see cref="Contexts"/> of an action with the given scope.
    /// </summary>
    public static CrudActionContext DefaultContextsFor(CrudActionScope scope) =>
        scope == CrudActionScope.None
            ? CrudActionContext.ListToolbar | CrudActionContext.Card | CrudActionContext.ListPart
            : CrudActionContext.All;
}
