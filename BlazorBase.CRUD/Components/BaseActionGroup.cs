using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Icon = Microsoft.FluentUI.AspNetCore.Components.Icon;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Declarative child component for defining an action group inside a BaseList or BaseCard.
/// Collects child BaseAction components and registers the group with the parent collector.
/// </summary>
public class BaseActionGroup<TModel> : ComponentBase, IActionCollector<TModel> where TModel : class
{
    [Parameter, EditorRequired]
    public string Name { get; set; } = default!;

    [Parameter]
    public string? Caption { get; set; }

    [Parameter]
    public Icon? Icon { get; set; }

    [Parameter]
    public CrudActionContext Contexts { get; set; } = CrudActionContext.All;

    [Parameter]
    public int Order { get; set; }

    [Parameter]
    public Func<CrudActionVisibilityArgs<TModel>, Task<bool>>? Visible { get; set; }

    [Parameter]
    public Func<ClaimsPrincipal, bool>? VisibleForRoles { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [CascadingParameter]
    private IActionGroupCollector<TModel>? GroupCollector { get; set; }

    private readonly List<CrudAction<TModel>> collectedActions = [];

    void IActionCollector<TModel>.AddAction(CrudAction<TModel> action)
    {
        collectedActions.Add(action);
    }

    protected override void OnInitialized()
    {
        GroupCollector?.AddActionGroup(ToGroup());
    }

    private CrudActionGroup<TModel> ToGroup() => new()
    {
        Name = Name,
        Caption = Caption,
        Icon = Icon,
        Contexts = Contexts,
        Order = Order,
        Visible = Visible,
        VisibleForRoles = VisibleForRoles,
        Actions = collectedActions
    };

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<IActionCollector<TModel>>>(0);
        builder.AddComponentParameter(1, "Value", (IActionCollector<TModel>)this);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", ChildContent);
        builder.CloseComponent();
    }
}
