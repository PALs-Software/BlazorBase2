using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The mobile side of the list: below a container width the table is replaced by tiles, see
/// <see cref="BaseListCardView{TModel}"/> and <see cref="MobileCardPlanResolver"/>.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    #region Mobile Parameters

    /// <summary>
    /// When the list shows tiles instead of its table: automatically below <see cref="MobileBreakpoint"/> (the
    /// default), always, or never.
    /// </summary>
    [Parameter]
    public BaseListMobileLayout MobileLayout { get; set; } = BaseListMobileLayout.Auto;

    /// <summary>
    /// The width of the list's container, in pixels, below which tiles replace the table. Falls back to
    /// <see cref="BaseListConfiguration{TModel}.MobileBreakpoint"/>, then to
    /// <see cref="MobileCardPlanResolver.DefaultBreakpoint"/>; 0 keeps the table at every width.
    /// </summary>
    [Parameter]
    public int? MobileBreakpoint { get; set; }

    /// <summary>
    /// Which properties a tile shows and the part each plays. Takes precedence over
    /// <see cref="BaseListConfiguration{TModel}.MobileCard"/>; without either the column roles, the entity
    /// attributes and finally the data types decide.
    /// </summary>
    [Parameter]
    public MobileCardConfiguration<TModel>? MobileCard { get; set; }

    /// <summary>
    /// Custom markup for the body of a tile. The tile keeps its frame: tapping it, the more button, the selection
    /// mark and the action row.
    /// </summary>
    [Parameter]
    public RenderFragment<TModel>? MobileTemplate { get; set; }

    /// <summary>
    /// The height in pixels of every tile. The tiles of a virtualized list must all have the same height; without a value
    /// it is worked out from what the tile shows, or, for a tile with a custom body, set to 120. A custom body that
    /// needs more room than that has to say so here, as what does not fit is cut off.
    /// </summary>
    [Parameter]
    public int? MobileItemHeight { get; set; }

    #endregion

    #region Mobile State

    private IJSObjectReference? MobileJsModule { get; set; }
    private bool IsNarrowContainer { get; set; }
    private ItemsProviderDelegate<TModel>? TileProvider { get; set; }

    /// <summary>
    /// Serves the tile view the tiles it asks for, see <see cref="ReadRangeAsync"/>.
    /// </summary>
    private async ValueTask<ItemsProviderResult<TModel>> ProvideTileItemsAsync(ItemsProviderRequest request)
    {
        var items = await ReadRangeAsync(request.StartIndex, request.Count, request.CancellationToken, debounce: true);

        return new ItemsProviderResult<TModel>(items, Math.Max(0, RangeCache.TotalCount));
    }

    private bool IsMobileLayout => MobileLayout switch
    {
        BaseListMobileLayout.Always => true,
        BaseListMobileLayout.Never => false,
        _ => IsNarrowContainer
    };

    private int EffectiveMobileBreakpoint => MobileBreakpoint ?? Configuration?.MobileBreakpoint ?? MobileCardPlanResolver.DefaultBreakpoint;

    private MobileCardConfiguration<TModel>? EffectiveMobileCard => MobileCard ?? Configuration?.MobileCard;

    private MobileCardPlan<TModel> MobilePlan =>
        MobileCardPlanResolver.Resolve([.. VisibleColumnsForUser], EffectiveMobileCard, IsColumnVisibleForUser);

    private IEnumerable<string> MobileExtraSelectNames =>
        MobileLayout == BaseListMobileLayout.Never ? [] : MobilePlan.ExtraColumns.Select(column => column.PropertyName);

    #endregion

    /// <summary>
    /// Starts watching the container width and takes the first answer, so the first page is loaded into the
    /// right layout.
    /// </summary>
    private async Task InitializeMobileLayoutAsync()
    {
        if (MobileLayout == BaseListMobileLayout.Never)
            return;

        var observesLayout = MobileLayout == BaseListMobileLayout.Auto && EffectiveMobileBreakpoint > 0;

        if (MobileLayout == BaseListMobileLayout.Auto && !observesLayout)
            return;

        MobileJsModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/baseListMobile.js");

        if (IsDisposed)
        {
            await DisposeMobileLayoutAsync();
            return;
        }

        await MobileJsModule.InvokeVoidAsync("observeTiles", GridContainerRef, SelfReference);

        if (observesLayout)
            IsNarrowContainer = await MobileJsModule.InvokeAsync<bool>("observeLayout", GridContainerRef, SelfReference, EffectiveMobileBreakpoint);
    }

    /// <summary>
    /// Called by the layout script when the container crossed the breakpoint.
    /// </summary>
    [JSInvokable]
    public async Task OnMobileLayoutChangedAsync(bool narrow)
    {
        if (IsDisposed || narrow == IsNarrowContainer)
            return;

        IsNarrowContainer = narrow;
        ContextMenuOpen = false;
        await InvokeAsync(StateHasChanged);
    }

    private async ValueTask DisposeMobileLayoutAsync()
    {
        var module = MobileJsModule;
        MobileJsModule = null;

        if (module is null)
            return;

        try
        {
            await module.InvokeVoidAsync("disposeLayout", GridContainerRef);
            await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }

    /// <summary>
    /// The height every tile has, in rem and without its border, see <see cref="BaseListTileMetrics"/>.
    /// </summary>
    private double ResolveTileHeight(MobileCardPlan<TModel> plan, MobileCardConfiguration<TModel>? card)
    {
        if (MobileItemHeight is { } explicitHeight)
            return Math.Max(1, (explicitHeight - BaseListTileMetrics.BorderPixels) / BaseListTileMetrics.RootFontSizePixels);

        if (MobileTemplate is not null || card?.Template is not null)
            return BaseListTileMetrics.CustomBodyHeightRem;

        var hasActions = plan.ActionColumns.Any(column => column.Template is not null) || card?.Actions is not null;

        return BaseListTileMetrics.Resolve(plan, hasActions);
    }

    private BaseListCardViewModel<TModel> BuildMobileView()
    {
        var card = EffectiveMobileCard;
        var sort = CurrentSorts.FirstOrDefault();
        var plan = MobilePlan;

        return new BaseListCardViewModel<TModel>
        {
            ItemsProvider = TileProvider ??= ProvideTileItemsAsync,
            TotalCount = TotalCount,
            DataVersion = DataVersion,
            TileHeight = ResolveTileHeight(plan, card),
            Plan = plan,
            Localizer = FrameworkLocalizer,
            Template = MobileTemplate ?? card?.Template,
            Actions = card?.Actions,
            IsClickable = CanOpen || OnItemSelected.HasDelegate || ShowsSelectionColumn,
            SelectionActive = ShowsSelectionColumn,
            IsSelected = IsSelected,
            IsBlocked = item => BlockedItems.Contains(item),
            IndexOf = RangeCache.IndexOf,
            OnOpen = RerenderAfterAsync<TModel>(OnMobileItemOpenedAsync),
            OnToggleSelect = item =>
            {
                ToggleMobileSelection(item);
                StateHasChanged();
            },
            OnMenu = RerenderAfterAsync<TModel, MouseEventArgs>(OnRowMenuButtonClickedAsync),
            ResolveFallbackTitle = ResolveItemDisplayName,
            ResolveLabel = ResolveColumnTitle,
            RenderValue = RenderMobileValue,
            BadgeText = ResolveMobileBadgeText,
            SortOptions = BuildMobileSortOptions(),
            SortProperty = sort?.PropertyName ?? String.Empty,
            SortDescending = sort?.Direction == Models.SortDirection.Descending,
            OnSortChanged = RerenderAfterAsync<string, bool>(OnMobileSortChangedAsync)
        };
    }

    /// <summary>
    /// Wraps a tile callback so the list re-renders when it is done: the callback is bound to the tile view, so
    /// the framework would otherwise re-render only that, and not the list whose state the callback changed.
    /// </summary>
    private Func<TArg, Task> RerenderAfterAsync<TArg>(Func<TArg, Task> handler) => async argument =>
    {
        await handler(argument);
        await InvokeAsync(StateHasChanged);
    };

    private Func<TFirst, TSecond, Task> RerenderAfterAsync<TFirst, TSecond>(Func<TFirst, TSecond, Task> handler) => async (first, second) =>
    {
        await handler(first, second);
        await InvokeAsync(StateHasChanged);
    };

    /// <summary>
    /// What tapping a tile does: it opens the item the way a click on the open column does (the edit card, or the
    /// read-only card when the user may only read), except while rows are being picked, where a tap adds the tile to
    /// or takes it out of the selection because there is no modifier key on a touch screen.
    /// </summary>
    private async Task OnMobileItemOpenedAsync(TModel item)
    {
        if (ShowsSelectionColumn)
        {
            ToggleMobileSelection(item);
            return;
        }

        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(item);

        await OpenItemAsync(item);
    }

    /// <summary>
    /// Adds a tile to the selection or takes it out. Taking out the last one ends the picking, which on a phone is
    /// the way back out besides "Deselect all".
    /// </summary>
    private void ToggleMobileSelection(TModel item)
    {
        ToggleSelection(item);
        SelectionBarHint = null;

        if (SelectedItems.Count == 0)
            ClearSelection();
    }

    /// <summary>
    /// A long press on a tile, reported by the tile script with the position of the tile: it starts the picking with
    /// that tile selected, or, while rows are being picked, toggles it like a tap.
    /// </summary>
    [JSInvokable]
    public async Task OnTileLongPressAsync(int index)
    {
        if (IsDisposed || ContextMenuOpen || IsCardDialogOpen)
            return;

        await InvokeAsync(async () =>
        {
            await HandleTileLongPressAsync(index);
            StateHasChanged();
        });
    }

    internal async Task HandleTileLongPressAsync(int index)
    {
        if (!IsMobileLayout || !IsSelectionAvailable)
            return;

        var rows = await FetchRowsAsync(index, 1);

        if (rows.Count == 0)
            return;

        var item = rows[0];

        if (ShowsSelectionColumn)
        {
            ToggleMobileSelection(item);
            return;
        }

        if (!IsSelected(item))
            ToggleSelection(item);

        SelectionBarHint = null;
        EnterMultiSelectMode();
    }

    #region Selection bar

    private string? SelectionBarHint { get; set; }
    private IDisposable? SelectionBackGuard { get; set; }

    /// <summary>
    /// Whether the bar with the count and the item-bound actions is shown: on the tile layout while rows are being picked.
    /// </summary>
    private bool ShowsMobileSelectionBar => IsMobileLayout && ShowsSelectionColumn;

    private bool HasItemBoundToolbarActions => ToolbarActionGroups.Any(group => group.Actions.Any(action => action.Scope != CrudActionScope.None));

    private bool HasItemIndependentToolbarActions => ToolbarActionGroups.Any(group => group.Actions.Any(action => action.Scope == CrudActionScope.None));

    private IEnumerable<CrudActionGroup<TModel>> ToolbarActionGroups =>
        ResolvedActionGroups.Where(group => group.Contexts.HasFlag(CrudActionContext.ListToolbar));

    /// <summary>
    /// On the tile layout the item-bound actions live in the selection bar and the header keeps the ones that do not
    /// depend on a row.
    /// </summary>
    private bool ShowsHeaderToolbar => HasToolbarActions && (!IsMobileLayout || HasItemIndependentToolbarActions);

    private ToolbarActionFilter HeaderToolbarFilter => IsMobileLayout ? ToolbarActionFilter.ItemIndependent : ToolbarActionFilter.All;

    private string SelectionBarDeleteLabel => string.Format(FrameworkLocalizer["DeleteCount"].Value, SelectedItems.Count);

    private void OnSelectionBarReasonRequested(string reason)
    {
        SelectionBarHint = reason;
    }

    private async Task OnSelectionBarDeleteAsync()
    {
        SelectionBarHint = null;
        await OnDeleteSelectedAsync();
    }

    /// <summary>
    /// While rows are being picked on a phone the back button (or the back gesture) ends the picking instead of
    /// leaving the page; links of the page still navigate.
    /// </summary>
    private void SyncSelectionBackGuard()
    {
        var wanted = !IsDisposed && ShowsMobileSelectionBar;

        if (wanted == (SelectionBackGuard is not null))
            return;

        if (!wanted)
        {
            SelectionBackGuard?.Dispose();
            SelectionBackGuard = null;
            return;
        }

        try
        {
            SelectionBackGuard = Navigation.RegisterLocationChangingHandler(OnSelectionLocationChangingAsync);
        }
        catch (NotSupportedException)
        {
            SelectionBackGuard = null;
        }
    }

    private async ValueTask OnSelectionLocationChangingAsync(LocationChangingContext context)
    {
        if (IsDisposed || !ShowsMobileSelectionBar || context.IsNavigationIntercepted)
            return;

        context.PreventNavigation();

        await InvokeAsync(() =>
        {
            ClearSelection();
            StateHasChanged();
        });
    }

    #endregion

    private List<MobileSortOption> BuildMobileSortOptions()
    {
        var options = new List<MobileSortOption> { new(String.Empty, FrameworkLocalizer["SortDefault"].Value) };

        foreach (var column in VisibleColumnsForUser)
        {
            if (!column.Sortable || column.MobileRole == MobileRole.Actions || MobileCardPlanResolver.LooksLikeActionColumn(column))
                continue;

            if (options.Any(option => option.PropertyName == column.PropertyName))
                continue;

            options.Add(new MobileSortOption(column.PropertyName, ResolveColumnTitle(column)));
        }

        return options.Count > 1 ? options : [];
    }

    /// <summary>
    /// Sorts by a property from the tile list's sort selector, or goes back to the list's own order when the
    /// property is empty. Like a header click on the table it sorts on the data provider, not on the loaded page.
    /// </summary>
    private async Task OnMobileSortChangedAsync(string property, bool descending)
    {
        CurrentSorts = property.Length == 0
            ? []
            : [new SortDescriptor { PropertyName = property, Direction = descending ? Models.SortDirection.Descending : Models.SortDirection.Ascending }];

        await LoadDataAsync();
        await ScrollGridToTopAsync();
    }

    private RenderFragment RenderMobileValue(PropertyColumnConfig<TModel> column, TModel item)
    {
        if (column.Template is not null)
            return column.Template(item);

        if (ResolveCustomDisplay(column) is { } displayType)
            return RenderCustomDisplayCell(displayType, column, item);

        var text = ResolveCellText(column, item);

        return builder => builder.AddContent(0, text);
    }

    /// <summary>
    /// The text of a badge: the column title for a boolean, whatever its state (the tile shows the state as a mark),
    /// the displayed value for anything else.
    /// </summary>
    private string? ResolveMobileBadgeText(PropertyColumnConfig<TModel> column, TModel item)
    {
        if (IsBooleanColumn(column))
            return ResolveColumnTitle(column);

        return ResolveCellText(column, item);
    }
}
