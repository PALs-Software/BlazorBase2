using System.Security.Claims;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Localization;

namespace BlazorBase.CRUD.Components;

public partial class BaseActionToolbar<TModel> : ComponentBase where TModel : class
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    #endregion

    /// <summary>
    /// Lets the declared actions collapse into a "more actions" menu when the row they share with other controls
    /// runs out of width, and renders multi-action groups as the toolbar's own menu instead of a Fluent menu button.
    /// The list header sets it; a card header keeps the plain wrapping toolbar.
    /// </summary>
    [Parameter]
    public bool AllowOverflow { get; set; }

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

    /// <summary>
    /// Which of the declared actions this toolbar shows: all of them, only the ones that work on rows, or only the ones
    /// that do not. The list's tile layout splits its header and its selection bar this way.
    /// </summary>
    [Parameter]
    public ToolbarActionFilter ItemFilter { get; set; } = ToolbarActionFilter.All;

    /// <summary>
    /// Raised with the reason a disabled action gives when the user taps it, which is how a touch screen, where there is
    /// no tooltip to hover, learns why an action is not available.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnReasonRequested { get; set; }

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
    private const string MoreKey = "more";
    private IJSObjectReference? module;
    private bool observing;
    private string? openMenuKey;
    private string? restoreFocusKey;
    private ContextMenuAnchor? menuAnchor;
    private double menuLeft;
    private double menuTop;
    private double? menuMaxHeight;
    private bool menuPositioned;
    private bool shouldPositionMenu;
    private bool shouldFocusMenu;

    private ElementReference RootElement { get; set; }
    private ElementReference MenuElement { get; set; }

    private IEnumerable<CrudActionGroup<TModel>> OpenMenuGroups =>
        openMenuKey == MoreKey ? VisibleGroups : VisibleGroups.Where(group => GroupKeyOf(group) == openMenuKey);

    private string OpenMenuLabel => openMenuKey == MoreKey
        ? FrameworkLocalizer["MoreActions"].Value
        : ResolveText(OpenMenuGroups.FirstOrDefault()?.Caption ?? OpenMenuGroups.FirstOrDefault()?.Name);

    private string MenuStyle
    {
        get
        {
            if (menuAnchor is null)
                return string.Empty;

            var style = FormattableString.Invariant($"left: {menuLeft}px; top: {menuTop}px;");

            return menuMaxHeight is { } maxHeight
                ? style + FormattableString.Invariant($" max-height: {maxHeight}px;")
                : style;
        }
    }

    private string GroupKeyOf(CrudActionGroup<TModel> group) => $"group-{VisibleGroups.IndexOf(group)}";

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

        if (openMenuKey is not null && !OpenMenuGroups.Any())
            openMenuKey = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (AllowOverflow && !observing)
        {
            observing = true;
            module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/BlazorBase.CRUD/js/baseActionToolbar.js");
            await module.InvokeVoidAsync("observe", RootElement);
        }

        await PositionMenuIfJustOpenedAsync();
        await FocusMenuIfJustOpenedAsync();
        await RestoreFocusIfMenuJustClosedAsync();
    }

    private async Task OpenMenuAsync(string key)
    {
        if (openMenuKey == key)
        {
            await CloseMenuAsync();
            return;
        }

        var box = module is null ? null : await module.InvokeAsync<double[]?>("measure", RootElement, key);

        menuAnchor = box is { Length: >= 4 } ? new ContextMenuAnchor(box[0], box[1], box[2], box[3]) : null;
        menuLeft = menuAnchor?.Left ?? 0;
        menuTop = menuAnchor?.Bottom ?? 0;
        menuMaxHeight = null;
        menuPositioned = false;
        openMenuKey = key;
        shouldPositionMenu = true;
    }

    private async Task PositionMenuIfJustOpenedAsync()
    {
        if (!shouldPositionMenu || openMenuKey is null)
            return;

        shouldPositionMenu = false;

        var measurement = module is null ? null : await module.InvokeAsync<double[]?>("measureMenu", MenuElement);

        if (measurement is { Length: >= 4 } && menuAnchor is { } anchor)
        {
            var placement = BaseListContextMenuPlacement.Compute(anchor, measurement[0], measurement[1], measurement[2], measurement[3]);

            menuLeft = placement.Left;
            menuTop = placement.Top;
            menuMaxHeight = placement.MaxHeight;
        }

        menuPositioned = true;
        shouldFocusMenu = true;
        StateHasChanged();
    }

    private async Task FocusMenuIfJustOpenedAsync()
    {
        if (!shouldFocusMenu || openMenuKey is null)
            return;

        shouldFocusMenu = false;
        await MenuElement.FocusAsync(preventScroll: true);

        if (module is not null)
            await module.InvokeVoidAsync("attachMenuKeys", MenuElement);
    }

    private async Task RestoreFocusIfMenuJustClosedAsync()
    {
        if (restoreFocusKey is null)
            return;

        var key = restoreFocusKey;
        restoreFocusKey = null;

        if (module is not null)
            await module.InvokeVoidAsync("focusTrigger", RootElement, key);
    }

    private async Task CloseMenuAsync()
    {
        if (openMenuKey is null)
            return;

        restoreFocusKey = openMenuKey;
        await CloseMenuQuietlyAsync();
    }

    private async Task CloseMenuQuietlyAsync()
    {
        openMenuKey = null;
        shouldPositionMenu = false;
        shouldFocusMenu = false;

        if (blockedAction is not null)
        {
            blockedAction = null;
            await RaiseBlockedItemsAsync([]);
        }
    }

    private async Task OnMenuKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseMenuAsync();
    }

    private async Task OnMenuItemClickedAsync(CrudAction<TModel> action)
    {
        if (!views.TryGetValue(action, out var view))
            return;

        if (!view.State.IsEnabled)
        {
            if (OnReasonRequested.HasDelegate)
            {
                await CloseMenuQuietlyAsync();
                await OnDisabledItemClickedAsync(action);
            }

            return;
        }

        await CloseMenuQuietlyAsync();
        await InvokeActionAsync(action);
    }

    public async ValueTask DisposeAsync()
    {
        if (module is null)
            return;

        try
        {
            await module.InvokeVoidAsync("unobserve", RootElement);
            await module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException or InvalidOperationException)
        {
        }
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
                var shown = groupShown && actionVisibility.GetValueOrDefault(action) && MatchesFilter(action);
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

    private bool MatchesFilter(CrudAction<TModel> action) => ItemFilter switch
    {
        ToolbarActionFilter.ItemBound => action.Scope != CrudActionScope.None,
        ToolbarActionFilter.ItemIndependent => action.Scope == CrudActionScope.None,
        _ => true
    };

    private async Task OnDisabledItemClickedAsync(CrudAction<TModel> action)
    {
        if (!OnReasonRequested.HasDelegate || !views.TryGetValue(action, out var view) || view.State.IsEnabled || string.IsNullOrEmpty(view.Reason))
            return;

        await OnReasonRequested.InvokeAsync(view.Reason);
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
