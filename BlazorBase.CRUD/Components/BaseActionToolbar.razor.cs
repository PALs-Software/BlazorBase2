using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Localization;

namespace BlazorBase.CRUD.Components;

public partial class BaseActionToolbar<TModel> : ComponentBase where TModel : class
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    [Parameter]
    public List<CrudActionGroup<TModel>> ActionGroups { get; set; } = [];

    [Parameter]
    public CrudActionContext Context { get; set; }

    [Parameter]
    public TModel? Item { get; set; }

    [Parameter]
    public IReadOnlyCollection<TModel> SelectedItems { get; set; } = [];

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public EventCallback OnActionExecuted { get; set; }

    /// <summary>
    /// Raised with the selected rows that block an action while the pointer or the keyboard is on that action's
    /// disabled button, and with an empty collection when it leaves, so a list can mark the rows that are the problem.
    /// </summary>
    [Parameter]
    public EventCallback<IReadOnlyCollection<TModel>> OnBlockedItemsChanged { get; set; }

    private sealed record ActionView(bool Shown, CrudActionState<TModel> State, string? Reason, string ReasonId);

    private static readonly CrudActionState<TModel> EmptyState = new();

    private RenderFragment? DynamicComponentFragment { get; set; }
    private ClaimsPrincipal? CurrentUser { get; set; }
    private List<CrudActionGroup<TModel>> VisibleGroups { get; set; } = [];

    private readonly string instanceId = Guid.NewGuid().ToString("N")[..8];
    private readonly Dictionary<CrudAction<TModel>, ActionView> views = [];
    private readonly Dictionary<CrudAction<TModel>, bool> actionVisibility = [];
    private readonly Dictionary<CrudActionGroup<TModel>, bool> groupVisibility = [];
    private List<TModel> visibilitySelection = [];
    private object? visibilityGroups;
    private object? visibilityItem;
    private ClaimsPrincipal? visibilityUser;
    private CrudActionContext visibilityContext;
    private int visibilityActionCount = -1;
    private CrudAction<TModel>? blockedAction;
    private IReadOnlyList<TModel> raisedBlockedItems = [];

    private IStringLocalizer FrameworkLocalizer => LocalizerResolver.ResolveFramework(ServiceProvider);

    protected override async Task OnInitializedAsync()
    {
        var authStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authStateProvider is not null)
        {
            var authState = await authStateProvider.GetAuthenticationStateAsync();
            CurrentUser = authState.User;
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var selection = CrudActionEvaluator<TModel>.ResolveSelection(Item, SelectedItems);

        if (HasVisibilityInputChanged(selection))
            await EvaluateVisibilityAsync(selection);

        BuildViews(selection);
        await SyncBlockedItemsAsync();
    }

    private bool HasVisibilityInputChanged(IReadOnlyList<TModel> selection)
    {
        var actionCount = ActionGroups.Sum(group => group.Actions.Count);

        var unchanged = ReferenceEquals(visibilityGroups, ActionGroups)
            && ReferenceEquals(visibilityItem, Item)
            && ReferenceEquals(visibilityUser, CurrentUser)
            && visibilityContext == Context
            && visibilityActionCount == actionCount
            && visibilitySelection.Count == selection.Count
            && visibilitySelection.Zip(selection).All(pair => ReferenceEquals(pair.First, pair.Second));

        if (unchanged)
            return false;

        visibilityGroups = ActionGroups;
        visibilityItem = Item;
        visibilityUser = CurrentUser;
        visibilityContext = Context;
        visibilityActionCount = actionCount;
        visibilitySelection = [.. selection];

        return true;
    }

    private async Task EvaluateVisibilityAsync(IReadOnlyList<TModel> selection)
    {
        actionVisibility.Clear();
        groupVisibility.Clear();

        foreach (var group in ActionGroups)
        {
            groupVisibility[group] = await CrudActionEvaluator<TModel>.IsGroupVisibleAsync(group, Context, CurrentUser, Item, selection);

            foreach (var action in group.Actions)
                actionVisibility[action] = await CrudActionEvaluator<TModel>.IsVisibleAsync(action, Context, CurrentUser, Item, selection);
        }
    }

    private void BuildViews(IReadOnlyList<TModel> selection)
    {
        views.Clear();
        var visibleGroups = new List<CrudActionGroup<TModel>>();
        var counter = 0;

        foreach (var group in ActionGroups.OrderBy(g => g.Order))
        {
            var anyShown = false;
            var groupShown = groupVisibility.GetValueOrDefault(group);

            foreach (var action in group.Actions)
            {
                var shown = groupShown && actionVisibility.GetValueOrDefault(action);
                var state = shown ? CrudActionEvaluator<TModel>.Evaluate(action, selection) : EmptyState;
                var reason = shown ? CrudActionEvaluator<TModel>.DescribeBlock(action, state, key => FrameworkLocalizer[key].Value, ResolveText) : null;

                views[action] = new ActionView(shown, state, reason, $"base-action-reason-{instanceId}-{counter++}");
                anyShown |= shown;
            }

            if (anyShown)
                visibleGroups.Add(group);
        }

        VisibleGroups = visibleGroups;
    }

    private bool IsActionShown(CrudAction<TModel> action) => views.TryGetValue(action, out var view) && view.Shown;

    private ActionView ViewOf(CrudAction<TModel> action) => views[action];

    private async Task OnBlockingPointerEnterAsync(CrudAction<TModel> action)
    {
        var blocking = views.TryGetValue(action, out var view) ? view.State.BlockingItems : [];

        if (blocking.Count == 0)
            return;

        blockedAction = action;
        await RaiseBlockedItemsAsync(blocking);
    }

    private async Task OnBlockingPointerLeaveAsync(CrudAction<TModel> action)
    {
        if (!ReferenceEquals(blockedAction, action))
            return;

        blockedAction = null;
        await RaiseBlockedItemsAsync([]);
    }

    private async Task SyncBlockedItemsAsync()
    {
        if (blockedAction is null)
            return;

        var current = views.TryGetValue(blockedAction, out var view) ? view.State.BlockingItems : [];

        if (current.Count == 0)
            blockedAction = null;

        if (current.Count == raisedBlockedItems.Count && current.Zip(raisedBlockedItems).All(pair => ReferenceEquals(pair.First, pair.Second)))
            return;

        await RaiseBlockedItemsAsync(current);
    }

    private async Task RaiseBlockedItemsAsync(IReadOnlyList<TModel> items)
    {
        raisedBlockedItems = items;

        if (OnBlockedItemsChanged.HasDelegate)
            await OnBlockedItemsChanged.InvokeAsync(items);
    }

    private string ResolveText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        if (Localizer is not null)
            return Localizer[text]?.Value ?? text;

        return text;
    }

    private async Task InvokeActionAsync(CrudAction<TModel> action)
    {
        if (!views.TryGetValue(action, out var view) || !view.State.IsEnabled)
            return;

        if (action.DynamicComponent is not null)
        {
            RenderDynamicComponent(action, view.State);
            return;
        }

        if (action.Action is null)
            return;

        var eventArgs = CrudActionEvaluator<TModel>.BuildEventArgs(action, view.State.Targets, Item, ServiceProvider, Context);

        await action.Action(eventArgs);

        if (OnActionExecuted.HasDelegate)
            await OnActionExecuted.InvokeAsync();
    }

    private void RenderDynamicComponent(CrudAction<TModel> action, CrudActionState<TModel> state)
    {
        var componentConfig = action.DynamicComponent!;

        var eventArgs = CrudActionEvaluator<TModel>.BuildEventArgs(action, state.Targets, Item, ServiceProvider, Context);

        DynamicComponentFragment = builder =>
        {
            builder.OpenComponent(0, componentConfig.ComponentType);

            if (componentConfig.Parameters is not null)
            {
                foreach (var parameter in componentConfig.Parameters)
                    builder.AddComponentParameter(1, parameter.Key, parameter.Value);
            }

            builder.AddComponentParameter(2, "Args", eventArgs);
            builder.AddComponentParameter(3, "OnClose",
                EventCallback.Factory.Create(this, async () =>
                {
                    DynamicComponentFragment = null;

                    if (componentConfig.OnComponentClosed is not null)
                        await componentConfig.OnComponentClosed(eventArgs);

                    if (OnActionExecuted.HasDelegate)
                        await OnActionExecuted.InvokeAsync();

                    StateHasChanged();
                }));

            builder.CloseComponent();
        };

        StateHasChanged();
    }
}
