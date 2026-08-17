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
    public CrudActionContext Contexts { get; set; } = CrudActionContext.All;

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
            Contexts = Contexts,
            Order = Order,
            Visible = Visible,
            VisibleForRoles = VisibleForRoles,
            Action = OnExecute,
            IsBulkAction = IsBulkAction
        };

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
