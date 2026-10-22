using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Declarative child component for defining a single action inside a BaseActionGroup.
/// Registers itself with the parent group collector on initialization.
/// </summary>
public class BaseAction<TModel> : ComponentBase where TModel : class
{
    [Parameter]
    public string? Caption { get; set; }

    [Parameter]
    public string? ToolTip { get; set; }

    [Parameter]
    public Icon? Icon { get; set; }

    [Parameter]
    public Appearance Appearance { get; set; } = Appearance.Stealth;

    [Parameter]
    public CrudActionContext? Contexts { get; set; }

    [Parameter]
    public int Order { get; set; }

    [Parameter]
    public Func<CrudActionVisibilityArgs<TModel>, Task<bool>>? Visible { get; set; }

    [Parameter]
    public Func<ClaimsPrincipal, bool>? VisibleForRoles { get; set; }

    [Parameter]
    public Func<CrudActionEventArgs<TModel>, Task>? OnExecute { get; set; }

    [Parameter]
    public bool IsBulkAction { get; set; }

    /// <summary>
    /// Which rows the action works on. Wins over <see cref="IsBulkAction"/> when both are set.
    /// </summary>
    [Parameter]
    public CrudActionScope? Scope { get; set; }

    /// <summary>
    /// Per-item predicate that decides whether the action can run for a row.
    /// </summary>
    [Parameter]
    public Func<TModel, bool>? Enabled { get; set; }

    /// <summary>
    /// Static text explaining why a row blocks the action.
    /// </summary>
    [Parameter]
    public string? DisabledReason { get; set; }

    /// <summary>
    /// Per-item function explaining why a row blocks the action.
    /// </summary>
    [Parameter]
    public Func<TModel, string?>? DisabledReasonProvider { get; set; }

    [Parameter]
    public Type? DynamicComponentType { get; set; }

    [CascadingParameter]
    private IActionCollector<TModel>? ActionCollector { get; set; }

    protected override void OnInitialized()
    {
        ActionCollector?.AddAction(ToAction());
    }

    private CrudAction<TModel> ToAction()
    {
        var action = new CrudAction<TModel>
        {
            Caption = Caption,
            ToolTip = ToolTip,
            Icon = Icon,
            Appearance = Appearance,
            Order = Order,
            Visible = Visible,
            VisibleForRoles = VisibleForRoles,
            Action = OnExecute,
            IsBulkAction = IsBulkAction,
            Enabled = Enabled,
            DisabledReason = DisabledReason,
            DisabledReasonProvider = DisabledReasonProvider
        };

        if (Scope.HasValue)
            action.Scope = Scope.Value;

        if (Contexts.HasValue)
            action.Contexts = Contexts.Value;

        if (DynamicComponentType is not null)
        {
            action.DynamicComponent = new CrudActionComponentConfig<TModel>
            {
                ComponentType = DynamicComponentType
            };
        }

        return action;
    }
}
