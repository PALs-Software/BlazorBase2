using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Components.Filtering;
using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Display;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Navigation;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;
using BlazorBase.Components.Routing;
using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Services;

namespace BlazorBase.CRUD.Components;

public partial class BaseList<TModel> : ComponentBase, IColumnCollector<TModel>, IActionGroupCollector<TModel>, IFilterConfigCollector<TModel>, IAsyncDisposable where TModel : class, new()
{
    #region Injects

    [Inject]
    private IDialogService DialogService { get; set; } = default!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    #endregion

    private IBaseDataProvider<TModel> ResolvedDataProvider =>
        DataProvider ?? ServiceProvider.GetService<IBaseDataProvider<TModel>>()
        ?? throw new InvalidOperationException($"No DataProvider available for {typeof(TModel).Name}. Register via DI or pass as parameter.");

    #region Parameters

    [Parameter]
    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    [Parameter]
    public BaseListConfiguration<TModel>? Configuration { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool AllowAdd { get; set; } = true;

    [Parameter]
    public bool AllowEdit { get; set; } = true;

    [Parameter]
    public bool AllowDelete { get; set; } = true;

    [Parameter]
    public RenderFragment<TModel>? ContextMenuActions { get; set; }

    [Parameter]
    public IStringLocalizer? Localizer { get; set; }

    [Parameter]
    public BaseCardConfiguration<TModel>? CardConfiguration { get; set; }

    [Parameter]
    public Type? CardType { get; set; }

    [Parameter]
    public string? AddButtonText { get; set; }

    [Parameter]
    public string? EmptyText { get; set; }

    [Parameter]
    public List<FilterDescriptor>? AdditionalFilters { get; set; }

    [Parameter]
    public List<NavigationFilter>? NavigationFilters { get; set; }

    [Parameter]
    public List<CrudActionGroup<TModel>>? ActionGroups { get; set; }

    /// <summary>
    /// When true (the default), the open item is reflected in the URL as a query parameter so a card can be
    /// deep-linked, shared and closed with the browser Back button. Set false to opt a list out.
    /// </summary>
    [Parameter]
    public bool EnableDeepLinking { get; set; } = true;

    /// <summary>
    /// Name of the query parameter carrying the open item's id (default <c>item</c>). Override to avoid a
    /// collision when more than one deep-linked list renders on the same page.
    /// </summary>
    [Parameter]
    public string DeepLinkParameterName { get; set; } = "item";

    /// <summary>
    /// How many rows one request to the data provider asks for. The list is virtualized: only the rows near the
    /// visible part of the page are rendered and the data provider is asked for them in aligned chunks of this size
    /// (kept between 10 and the provider's page limit) while the page scrolls, so this is a fetch granularity and no
    /// longer the number of rows the list loads at start.
    /// </summary>
    [Parameter]
    public int PageSize { get; set; } = 50;

    /// <summary>
    /// The order the list shows until the user sorts it. Takes precedence over
    /// <see cref="BaseListConfiguration{TModel}.DefaultSorts"/> and
    /// <see cref="Attributes.DefaultSortAttribute"/>; without any of them rows come back by primary key.
    /// </summary>
    [Parameter]
    public List<SortDescriptor>? DefaultSorts { get; set; }

    #endregion

    #region Events

    [Parameter]
    public EventCallback<ItemEventArgs<TModel>> OnBeforeItemCreate { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterItemCreated { get; set; }

    [Parameter]
    public EventCallback<ItemEventArgs<TModel>> OnBeforeItemUpdate { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterItemUpdated { get; set; }

    [Parameter]
    public EventCallback<ItemEventArgs<TModel>> OnBeforeItemDelete { get; set; }

    [Parameter]
    public EventCallback<TModel> OnAfterItemDeleted { get; set; }

    [Parameter]
    public EventCallback<TModel> OnItemSelected { get; set; }

    #endregion

    #region Data State

    private List<PropertyColumnConfig<TModel>> CollectedColumns { get; } = [];
    private List<CrudActionGroup<TModel>> CollectedActionGroups { get; } = [];
    private List<FilterDescriptor> CurrentFilters { get; set; } = [];
    private List<SortDescriptor> CurrentSorts { get; set; } = [];
    private int TotalCount { get; set; }
    private bool IsLoading { get; set; }

    private List<PropertyColumnConfig<TModel>> ResolvedColumns =>
        Configuration?.Columns ?? CollectedColumns;

    private List<CrudActionGroup<TModel>> ResolvedActionGroups =>
        ActionGroups ?? Configuration?.ActionGroups ?? CollectedActionGroups;

    #endregion

    #region Access Rights

    private CrudRights ClassRightsForCurrentUser =>
        CurrentUser is null ? CrudRights.All : CrudAccessResolver.EvaluateClass(typeof(TModel), CurrentUser);

    private bool CanAdd => AllowAdd && (ClassRightsForCurrentUser & CrudRights.Insert) == CrudRights.Insert;

    private bool CanEdit => AllowEdit && (ClassRightsForCurrentUser & CrudRights.Modify) == CrudRights.Modify;

    private bool CanDelete => AllowDelete && (ClassRightsForCurrentUser & CrudRights.Delete) == CrudRights.Delete;

    private bool IsColumnVisibleForUser(PropertyColumnConfig<TModel> column)
    {
        if (!column.Visible)
            return false;

        if (CurrentUser is null)
            return true;

        var effective = CrudAccessResolver.EvaluateProperty(typeof(TModel), column.PropertyName, CurrentUser);
        effective = CrudAccessResolver.ApplyViewRights(effective, column.AccessRules, CurrentUser);
        return (effective & CrudRights.Read) == CrudRights.Read;
    }

    private IEnumerable<PropertyColumnConfig<TModel>> VisibleColumnsForUser =>
        ResolvedColumns.Where(IsColumnVisibleForUser);

    #endregion

    #region Selection State

    private HashSet<TModel> SelectedItems { get; } = new(ItemIdentityComparer<TModel>.Instance);
    private bool IsSelectionActive => SelectedItems.Count > 0;

    /// <summary>
    /// Whether selecting rows leads anywhere: only a delete or a bulk action consumes a selection, so a
    /// read-only list does not offer one.
    /// </summary>
    private bool IsSelectionAvailable =>
        CanDelete || ResolvedActionGroups.Any(group => group.Actions.Any(action => action.Scope != CrudActionScope.None));

    private HashSet<TModel> BlockedItems { get; set; } = new(ItemIdentityComparer<TModel>.Instance);
    private int BlockedVersion { get; set; }

    /// <summary>
    /// Marks the rows that block the toolbar action the pointer or keyboard is on.
    /// </summary>
    private void OnBlockedItemsChanged(IReadOnlyCollection<TModel> items)
    {
        BlockedItems = new HashSet<TModel>(items, ItemIdentityComparer<TModel>.Instance);
        BlockedVersion++;
    }

    private string? ResolveRowClass(TModel item) => BlockedItems.Contains(item) ? "base-list-row-blocked" : null;

    private bool HasContextMenuBaseEntries => CanEdit || CanDelete || IsSelectionAvailable;

    /// <summary>
    /// Whether a row has anything to offer in its menu; a list without entries gets no more-button column.
    /// </summary>
    private bool HasContextMenuEntries =>
        HasContextMenuBaseEntries || ContextMenuActions is not null || ContextMenuActionGroups.Count > 0;

    #endregion

    #region Context Menu State

    private TModel? ContextMenuItem { get; set; }
    private ContextMenuAnchor ContextMenuAnchorRect { get; set; }
    private double ContextMenuLeft { get; set; }
    private double ContextMenuTop { get; set; }
    private double? ContextMenuMaxHeight { get; set; }
    private bool ContextMenuOpen { get; set; }
    private bool ContextMenuPositioned { get; set; }
    private bool ShouldPositionContextMenu { get; set; }
    private bool ShouldFocusContextMenu { get; set; }
    private string? ShouldRestoreFocusKey { get; set; }
    private ElementReference ContextMenuElement { get; set; }
    private readonly string ContextMenuFocusKey = $"context-menu-{Guid.NewGuid()}";

    private string ContextMenuStyle
    {
        get
        {
            var style = FormattableString.Invariant($"left: {ContextMenuLeft}px; top: {ContextMenuTop}px;");

            return ContextMenuMaxHeight is { } maxHeight
                ? style + FormattableString.Invariant($" max-height: {maxHeight}px;")
                : style;
        }
    }

    #endregion

    #region Interaction State

    private bool LastShiftState { get; set; }
    private ElementReference GridContainerRef { get; set; }
    private FluentDataGrid<TModel>? DataGrid { get; set; }
    private GridItemsProvider<TModel>? GridProvider { get; set; }
    private Dictionary<PropertyColumnConfig<TModel>, GridSort<TModel>> SortByColumns { get; } = [];
    private int DataVersion { get; set; }
    private int ParametersVersion { get; set; }
    private int SelectionVersion { get; set; }

    /// <summary>
    /// What the rows on screen depend on. The rows are the expensive part of a long list, so they are rebuilt only when
    /// this changes and not for every render of the list, which a context menu, the filter panel or the loading
    /// indicator each cause.
    /// </summary>
    private object GridRenderKey => (
        DataVersion,
        ParametersVersion,
        SelectionVersion,
        BlockedVersion,
        IsMobileLayout,
        ShowsRows: HasRows || IsLoading,
        ResolvedColumns.Count,
        CurrentUser,
        Sort: CurrentSorts.FirstOrDefault()?.PropertyName + CurrentSorts.FirstOrDefault()?.Direction);

    private int RenderedVersion { get; set; }
    private int RefreshedVersion { get; set; }
    private int LoadVersion { get; set; }
    private bool ShouldSyncGridState { get; set; }
    private bool IsDisposed { get; set; }
    private bool IsCardDialogOpen { get; set; }
    private IJSObjectReference? JsModule { get; set; }
    private DotNetObjectReference<BaseList<TModel>>? SelfReference { get; set; }

    #endregion

    #region Deep Linking State

    private string? CurrentOpenKey { get; set; }
    private int DeepLinkTicket { get; set; }
    private IDialogReference? OpenItemDialog { get; set; }
    private bool DeepLinkSubscribed { get; set; }
    private string CardDialogFocusKey => $"card-dialog-{DeepLinkParameterName}";

    #endregion

    void IColumnCollector<TModel>.AddColumn(PropertyColumnConfig<TModel> column)
    {
        CollectedColumns.Add(column);
    }

    void IActionGroupCollector<TModel>.AddActionGroup(CrudActionGroup<TModel> group)
    {
        CollectedActionGroups.Add(group);
    }

    void IFilterConfigCollector<TModel>.RegisterFilterConfig(FilterConfig<TModel> filterConfig)
    {
        CollectedFilterConfigComponent = filterConfig;
    }

    private List<FilterDescriptor>? PreviousAdditionalFilters;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            await RefreshGridForRenderedItemsAsync();
            await SyncGridStateAsync();
            await PositionContextMenuIfJustOpenedAsync();
            await FocusContextMenuIfJustOpenedAsync();
            await RestoreFocusIfAnOverlayJustClosedAsync();
            return;
        }

        JsModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/baseListInterop.js");

        if (IsDisposed)
        {
            await JsModule.DisposeAsync();
            JsModule = null;
            return;
        }

        SelfReference = DotNetObjectReference.Create(this);
        await JsModule.InvokeVoidAsync("observeGrid", GridContainerRef, SelfReference, ResolveInitialFocusRestoreKey());

        await InitializeMobileLayoutAsync();
        await LoadCurrentUserAsync();
        ResolveFilterFields();
        await InitializeSavedFiltersAsync();
        await LoadDataAsync();

        if (EnableDeepLinking)
        {
            Navigation.LocationChanged += OnLocationChanged;
            DeepLinkSubscribed = true;
            await SyncDeepLinkAsync();
        }
    }

    /// <summary>
    /// The remembered focus to hand back once the first rows are on screen: a card that was just closed leaves
    /// the keyboard on the row it was opened from, but the router may have rebuilt this list in the meantime,
    /// so the rebuilt one has to put it back. A list that opens a card straight away has no use for it.
    /// </summary>
    private string? ResolveInitialFocusRestoreKey()
    {
        if (!EnableDeepLinking)
            return null;

        return string.IsNullOrEmpty(QueryStringReader.TryReadValue(Navigation.Uri, DeepLinkParameterName))
            ? CardDialogFocusKey
            : null;
    }
    /// <summary>
    /// Tells the grid script that new rows are on screen, so it measures the layout again.
    /// </summary>
    private async Task SyncGridStateAsync()
    {
        if (!ShouldSyncGridState || JsModule is null)
            return;

        ShouldSyncGridState = false;
        await JsModule.InvokeVoidAsync("updateGrid", GridContainerRef);
    }

    /// <summary>
    /// Measures the menu that just rendered and moves it to where it fits the viewport, then reveals it.
    /// </summary>
    /// <remarks>
    /// The menu first renders hidden at the anchor, because its size is only known once it exists. Without a
    /// measurement (no script) it stays where it was asked to open.
    /// </remarks>
    private async Task PositionContextMenuIfJustOpenedAsync()
    {
        if (!ShouldPositionContextMenu)
            return;

        ShouldPositionContextMenu = false;

        var measurement = JsModule is null
            ? null
            : await JsModule.InvokeAsync<double[]?>("measureElement", ContextMenuElement);

        if (measurement is { Length: >= 4 })
        {
            var placement = BaseListContextMenuPlacement.Compute(
                ContextMenuAnchorRect, measurement[0], measurement[1], measurement[2], measurement[3]);

            ContextMenuLeft = placement.Left;
            ContextMenuTop = placement.Top;
            ContextMenuMaxHeight = placement.MaxHeight;
        }

        ContextMenuPositioned = true;
        ShouldFocusContextMenu = true;
        StateHasChanged();
    }

    /// <summary>
    /// Puts the keyboard on the menu that just opened, so Escape and the arrow keys reach it.
    /// </summary>
    private async Task FocusContextMenuIfJustOpenedAsync()
    {
        if (!ShouldFocusContextMenu)
            return;

        ShouldFocusContextMenu = false;
        await ContextMenuElement.FocusAsync(preventScroll: true);
    }

    /// <summary>
    /// Puts the keyboard back where it stood before an overlay opened.
    /// </summary>
    private async Task RestoreFocusIfAnOverlayJustClosedAsync()
    {
        var key = ShouldRestoreFocusKey;

        if (key is null || JsModule is null)
            return;

        ShouldRestoreFocusKey = null;
        await JsModule.InvokeVoidAsync("restoreFocus", key);
    }

    protected override async Task OnParametersSetAsync()
    {
        ParametersVersion++;

        if (PreviousAdditionalFilters is not null && AdditionalFilters != PreviousAdditionalFilters)
            await LoadDataAsync();

        PreviousAdditionalFilters = AdditionalFilters;
    }

    #region Row Interaction Handlers

    private void OnMouseDown(MouseEventArgs e)
    {
        LastShiftState = e.ShiftKey;
    }

    private async Task OnRowClickedAsync(FluentDataGridRow<TModel> row)
    {
        if (row.Item is null)
            return;

        if (LastShiftState && IsSelectionAvailable)
        {
            ToggleSelection(row.Item);
            return;
        }

        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(row.Item);

        if (CanEdit)
            await OnEditClickedAsync(row.Item);
    }

    /// <summary>
    /// Remembers which item the pointer or keyboard last landed on, so the context menu can act on it.
    /// </summary>
    /// <remarks>
    /// The cell is tracked rather than the row because a FluentUI data-grid row is a
    /// <c>tr</c> with <c>display: contents</c>, which browsers cannot focus — its <c>OnRowFocus</c>
    /// therefore never fires and left the menu with nothing to act on. Focus lands on the
    /// <c>gridcell</c> instead, header cells included, which carry no item.
    /// </remarks>
    private void OnCellFocusedAsync(FluentDataGridCell<TModel> cell)
    {
        if (cell.Item is not null)
            ContextMenuItem = cell.Item;
    }

    #endregion

    #region Context Menu Handlers

    private async Task OnContextMenuAsync(MouseEventArgs e)
    {
        if (!HasContextMenuEntries)
            return;

        if (JsModule is not null)
        {
            var isOnRow = await JsModule.InvokeAsync<bool>("isPointOnDataRow", GridContainerRef, e.ClientX, e.ClientY);
            if (!isOnRow)
                return;
        }

        await OpenContextMenuAsync(ContextMenuAnchor.AtPoint(e.ClientX, e.ClientY));
    }

    /// <summary>
    /// Opens the same menu a right-click opens from the per-row more button, which is what a touch screen
    /// and a narrow window have: neither has a right mouse button.
    /// </summary>
    private async Task OnRowMenuButtonClickedAsync(TModel item, MouseEventArgs e)
    {
        ContextMenuItem = item;

        var anchor = JsModule is null
            ? null
            : await JsModule.InvokeAsync<double[]?>("getRowMenuButtonRect", e.ClientX, e.ClientY);

        await OpenContextMenuAsync(anchor is { Length: >= 4 }
            ? new ContextMenuAnchor(anchor[0], anchor[1], anchor[2], anchor[3])
            : ContextMenuAnchor.AtPoint(e.ClientX, e.ClientY));
    }

    private async Task OpenContextMenuAsync(ContextMenuAnchor anchor)
    {
        await BuildContextMenuSectionsAsync();

        await RememberFocusAsync(ContextMenuFocusKey);

        ContextMenuAnchorRect = anchor;
        ContextMenuLeft = anchor.Left;
        ContextMenuTop = anchor.Bottom;
        ContextMenuMaxHeight = null;
        ContextMenuPositioned = false;
        ContextMenuOpen = true;
        ShouldPositionContextMenu = true;
    }

    private void CloseContextMenu()
    {
        ContextMenuOpen = false;
    }

    /// <summary>
    /// Closes the menu without acting on it, and puts the keyboard back on the cell it came from.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="CloseContextMenu"/> because every other entry closes the menu on its
    /// way into something else — an edit dialog, a delete confirmation — and those manage their own
    /// focus. Pulling it back onto the grid underneath them would fight them for it.
    /// </remarks>
    private void DismissContextMenu()
    {
        if (!ContextMenuOpen)
            return;

        ContextMenuOpen = false;
        ShouldRestoreFocusKey = ContextMenuFocusKey;
    }

    /// <summary>
    /// Escape closes the menu. It opens over an overlay that swallows every click outside it, so
    /// without this the only way out was to hit that overlay with the mouse.
    /// </summary>
    private void OnContextMenuKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            DismissContextMenu();
    }

    /// <summary>
    /// Notes where the keyboard stood before an overlay takes focus, so closing it can put the caret
    /// back rather than dropping it at the top of the document.
    /// </summary>
    /// <remarks>
    /// The browser remembers the element, not Blazor: what the keyboard came from is a data-grid cell
    /// or a Fluent UI button, and neither hands out an <see cref="ElementReference"/> to focus back
    /// onto.
    /// </remarks>
    private async Task RememberFocusAsync(string key)
    {
        if (JsModule is null)
            return;

        await JsModule.InvokeVoidAsync("rememberFocus", key);
    }

    private async Task OnContextMenuEditAsync()
    {
        var item = ContextMenuItem;
        CloseContextMenu();
        if (item is null)
            return;

        await OnEditClickedAsync(item);
    }

    private async Task OnContextMenuDeleteAsync()
    {
        var item = ContextMenuItem;
        CloseContextMenu();
        if (item is null)
            return;

        if (SelectedItems.Count > 1 && IsSelected(item))
            await OnDeleteSelectedAsync();
        else
            await OnDeleteWithConfirmationAsync(item);
    }

    private void OnContextMenuToggleSelectAsync()
    {
        if (ContextMenuItem is null)
            return;

        ToggleSelection(ContextMenuItem);
        CloseContextMenu();
    }

    #endregion

    #region Selection

    private bool IsSelected(TModel item) => SelectedItems.Contains(item);

    private void ToggleSelection(TModel item)
    {
        SelectionVersion++;

        if (!SelectedItems.Remove(item))
            SelectedItems.Add(item);
    }

    private void SelectAll()
    {
        SelectionVersion++;

        foreach (var item in RangeCache.ResidentItems)
            SelectedItems.Add(item);

        CloseContextMenu();
    }

    private void DeselectAll()
    {
        SelectionVersion++;
        SelectedItems.Clear();
        CloseContextMenu();
    }

    #endregion

    #region Data Loading

    private List<string> BuildSelectList()
    {
        var selectFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Id" };

        foreach (var column in VisibleColumnsForUser)
            selectFields.Add(column.PropertyName);

        foreach (var displayKeyName in ResolveExplicitDisplayKeyNames())
            selectFields.Add(displayKeyName);

        foreach (var mobileName in MobileExtraSelectNames)
            selectFields.Add(mobileName);

        return [.. selectFields];
    }

    private IReadOnlyList<SortDescriptor> EffectiveSorts
    {
        get
        {
            if (CurrentSorts.Count > 0)
                return CurrentSorts;

            if (DefaultSorts is { Count: > 0 })
                return DefaultSorts;

            if (Configuration?.DefaultSorts is { Count: > 0 } configuredSorts)
                return configuredSorts;

            return ResolveDeclaredDefaultSorts();
        }
    }

    private static List<SortDescriptor> ResolveDeclaredDefaultSorts()
    {
        return [.. typeof(TModel)
            .GetCustomAttributes(typeof(Attributes.DefaultSortAttribute), inherit: true)
            .Cast<Attributes.DefaultSortAttribute>()
            .OrderBy(declared => declared.Order)
            .Select(declared => new SortDescriptor
            {
                PropertyName = declared.PropertyName,
                Direction = declared.Descending ? Models.SortDirection.Descending : Models.SortDirection.Ascending
            })];
    }

    /// <summary>
    /// Loads the rows the list shows: a new filter or sort starts at the top, a reload after a save, a delete or a
    /// refresh (<paramref name="keepLoadedCount"/>) brings back the part of the list that is on screen, so the user
    /// keeps their place. Further rows are fetched by the virtualizer while the page scrolls.
    /// </summary>
    private async Task LoadDataAsync(bool keepLoadedCount = false)
    {
        var version = ++LoadVersion;
        var window = ResolveReloadWindow(keepLoadedCount);
        var cache = new ListRangeCache<TModel>(ChunkSize, MaxCachedChunks);

        if (!keepLoadedCount)
        {
            RangeCache = cache;
            ServedStart = 0;
            ServedCount = 0;
        }

        if (keepLoadedCount && JsModule is not null)
            await JsModule.InvokeVoidAsync("holdFocus", GridContainerRef);

        IsLoading = true;
        StateHasChanged();

        try
        {
            var first = window.Start / cache.ChunkSize;
            await Task.WhenAll(StartFetches(cache, [.. Enumerable.Range(first, window.Count / cache.ChunkSize)]));

            if (version == LoadVersion && !IsDisposed && cache.TotalCount > 0 && window.Start >= cache.TotalCount)
            {
                ServedStart = 0;
                ServedCount = 0;
                await Task.WhenAll(StartFetches(cache, [0]));
            }
        }
        catch
        {
            if (version == LoadVersion)
                IsLoading = false;

            throw;
        }

        if (version != LoadVersion || IsDisposed)
            return;

        RangeCache = cache;
        TotalCount = Math.Max(0, cache.TotalCount);
        IsLoading = false;
        DataVersion++;
        ShouldSyncGridState = true;
        StateHasChanged();
    }

    /// <summary>
    /// Hands the grid the provider it reads its rows from, and records which load this render carried.
    /// </summary>
    /// <remarks>
    /// The grid compares its <c>ItemsProvider</c> by reference, so building the delegate inline made every
    /// single render look like a new data source to the grid: it re-queried, and the explicit refresh below
    /// then cancelled that and queried again. One cached delegate stops the churn; the version counter is
    /// what still tells a genuine reload apart, including loading a further page, which keeps the same
    /// collection and would otherwise go unnoticed.
    /// </remarks>
    private GridItemsProvider<TModel> CaptureRenderedProvider()
    {
        RenderedVersion = DataVersion;

        return GridProvider ??= ProvideGridItemsAsync;
    }

    /// <summary>
    /// Serves the grid the rows it asks for, and sends a sort the user picked in a column header to the data provider.
    /// </summary>
    /// <remarks>
    /// A header sort used to be the grid's own: it re-ordered whatever was already loaded, so the top rows were not the
    /// true ones. The request the grid hands over names the sorted column, which becomes the list's current sort, the
    /// rows are loaded again in that order from the data provider (the database, the HTTP endpoint or the in-memory
    /// list sort everything, not just a page) and the grid starts at its first row. The rows themselves are read from
    /// the range cache, which fetches the chunks the virtualized grid asks for, see <see cref="ReadRangeAsync"/>. The
    /// grid also asks for a window without a count after every refresh, which only feeds its own bookkeeping: that
    /// request is answered from the first chunk of the window, is not cancellable and does not count as the position
    /// of the user.
    /// </remarks>
    private async ValueTask<GridItemsProviderResult<TModel>> ProvideGridItemsAsync(GridItemsProviderRequest<TModel> request)
    {
        var requestedSorts = ResolveRequestedSorts(request);

        if (!HasSameSorts(requestedSorts, CurrentSorts))
        {
            CurrentSorts = requestedSorts;
            await LoadDataAsync();
            RefreshedVersion = DataVersion;
            await ScrollGridToTopAsync();
        }

        var token = request.Count is null ? CancellationToken.None : request.CancellationToken;
        var items = await ReadRangeAsync(request.StartIndex, request.Count ?? ChunkSize, token, debounce: false, remember: request.Count is not null);

        return GridItemsProviderResult.From(items, Math.Max(0, RangeCache.TotalCount));
    }

    private List<SortDescriptor> ResolveRequestedSorts(GridItemsProviderRequest<TModel> request)
    {
        if (request.SortByColumn?.SortBy is not { } sortBy)
            return [];

        var column = SortByColumns.FirstOrDefault(pair => ReferenceEquals(pair.Value, sortBy)).Key;
        if (column is null)
            return [];

        return
        [
            new SortDescriptor
            {
                PropertyName = column.PropertyName,
                Direction = request.SortByAscending ? Models.SortDirection.Ascending : Models.SortDirection.Descending
            }
        ];
    }

    private static bool HasSameSorts(List<SortDescriptor> left, List<SortDescriptor> right)
    {
        return left.Count == right.Count
            && left.Zip(right).All(pair => pair.First.PropertyName == pair.Second.PropertyName
                && pair.First.Direction == pair.Second.Direction);
    }

    /// <summary>
    /// Brings the grid back to its first row after a sort, which is the row the user sorted to find.
    /// </summary>
    private async Task ScrollGridToTopAsync()
    {
        if (JsModule is null || IsDisposed)
            return;

        try
        {
            await JsModule.InvokeVoidAsync("scrollGridToTop", GridContainerRef);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    /// <summary>
    /// Makes a virtualized grid read the collection it was just given.
    /// </summary>
    /// <remarks>
    /// It caches through its own virtualization layer and does not reliably notice that the collection
    /// was replaced, so a delete could leave it painting a removed row or claiming it had no data while
    /// rows were loaded. The comparison is against what the completed render actually passed, not
    /// against the current field: a render triggered elsewhere can finish between the reload and the
    /// render carrying its result, and a plain "refresh pending" flag was consumed by that one instead —
    /// which is why this was intermittent rather than broken outright.
    /// </remarks>
    private async Task RefreshGridForRenderedItemsAsync()
    {
        if (DataGrid is null || RefreshedVersion == RenderedVersion)
            return;

        RefreshedVersion = RenderedVersion;
        await DataGrid.RefreshDataAsync();
    }

    public async Task RefreshAsync()
    {
        await LoadDataAsync(keepLoadedCount: true);
    }

    #endregion

    #region Filtering

    private FilterConfig<TModel>? CollectedFilterConfigComponent { get; set; }
    private List<FilterFieldMetadata> FilterFields { get; set; } = [];
    private FilterGroupModel ActiveFilterModel { get; set; } = new();
    private FilterGroupModel? WorkingFilterModel { get; set; }
    private bool FilterPanelOpen { get; set; }
    private readonly string FilterPanelFocusKey = $"filter-panel-{Guid.NewGuid()}";

    private FilterConfiguration<TModel> ResolvedFilterConfig =>
        Configuration?.Filter ?? CollectedFilterConfigComponent?.BuildConfiguration() ?? new FilterConfiguration<TModel>();

    private bool IsFilteringEnabled => FilterFields.Count > 0;

    private int ActiveFilterCount => FilterModelConverter.CountConditions(ActiveFilterModel);

    private string FilterButtonLabel => ActiveFilterCount > 0
        ? $"{FrameworkLocalizer["Filter"].Value} ({ActiveFilterCount})"
        : FrameworkLocalizer["Filter"].Value;

    private void ResolveFilterFields()
    {
        FilterFields = FilterFieldResolver.Resolve(ResolvedFilterConfig, CurrentUser, ResolvedLocalizer, ServiceProvider);
    }

    private async Task OpenFilterPanelAsync()
    {
        await RememberFocusAsync(FilterPanelFocusKey);

        WorkingFilterModel = ActiveFilterModel.Clone();
        FilterPanelOpen = true;
    }

    private void CloseFilterPanel()
    {
        if (!FilterPanelOpen)
            return;

        FilterPanelOpen = false;
        WorkingFilterModel = null;
        SavedFilterRevision++;
        ShouldRestoreFocusKey = FilterPanelFocusKey;
    }

    private async Task OnFilterApplyAsync(FilterGroupModel root)
    {
        ActiveFilterModel = root;
        SavedFilterRevision++;

        var descriptor = FilterModelConverter.ToDescriptor(root);
        CurrentFilters = descriptor is null ? [] : [descriptor];

        FilterPanelOpen = false;
        WorkingFilterModel = null;
        ShouldRestoreFocusKey = FilterPanelFocusKey;
        await LoadDataAsync();
        await ScrollGridToTopAsync();
    }

    private async Task OnFilterResetAsync()
    {
        ActiveFilterModel = new FilterGroupModel();
        WorkingFilterModel = new FilterGroupModel();
        CurrentFilters = [];
        await LoadDataAsync();
        await ScrollGridToTopAsync();
    }

    #endregion

    #region CRUD Operations

    private async Task OnAddClickedAsync()
    {
        if (IsCardDialogOpen)
            return;

        var newItem = Activator.CreateInstance<TModel>();

        var eventArgs = new ItemEventArgs<TModel> { Item = newItem };

        if (OnBeforeItemCreate.HasDelegate)
        {
            await OnBeforeItemCreate.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        var dialogData = new BaseDialogData<TModel>
        {
            Model = newItem,
            DataProvider = ResolvedDataProvider,
            IsNew = true,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialogResult = await ShowCardDialogAsync(dialogData);

        if (dialogResult is { Cancelled: false, Data: TModel savedItem })
        {
            if (OnAfterItemCreated.HasDelegate)
                await OnAfterItemCreated.InvokeAsync(savedItem);

            await LoadDataAsync(keepLoadedCount: true);
        }
    }

    private async Task OnEditClickedAsync(TModel item)
    {
        if (EnableDeepLinking && GetItemId(item) is { } deepLinkId)
        {
            await RememberFocusAsync(CardDialogFocusKey);
            Navigation.NavigateTo(Navigation.GetUriWithQueryParameter(DeepLinkParameterName, deepLinkId.ToString()));
            return;
        }

        if (IsCardDialogOpen)
            return;

        var eventArgs = new ItemEventArgs<TModel> { Item = item };

        if (OnBeforeItemUpdate.HasDelegate)
        {
            await OnBeforeItemUpdate.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        var editModel = await LoadFullEntityAsync(item);

        var dialogData = new BaseDialogData<TModel>
        {
            Model = editModel,
            DataProvider = ResolvedDataProvider,
            IsNew = false,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialogResult = await ShowCardDialogAsync(dialogData);

        if (dialogResult is { Cancelled: false, Data: TModel updatedItem })
        {
            if (OnAfterItemUpdated.HasDelegate)
                await OnAfterItemUpdated.InvokeAsync(updatedItem);

            await RefreshEditedItemAsync(updatedItem);
        }
    }

    /// <summary>
    /// Opens the card dialog for an item the list itself hosts (a new item, or an edit without deep
    /// linking), and takes the keyboard back to where it came from once it closes.
    /// </summary>
    /// <remarks>
    /// Only one card is ever open: a second click or Enter while the first is still opening must not stack
    /// another dialog on it, so the flag is set before the first await.
    /// </remarks>
    private async Task<DialogResult> ShowCardDialogAsync(BaseDialogData<TModel> dialogData)
    {
        IsCardDialogOpen = true;

        try
        {
            await RememberFocusAsync(CardDialogFocusKey);

            var dialog = await DialogService.ShowDialogAsync<BaseDialog<TModel>>(dialogData, BuildCardDialogParameters());
            return await dialog.Result;
        }
        finally
        {
            IsCardDialogOpen = false;
            RestoreCardDialogFocus();
        }
    }

    private void RestoreCardDialogFocus()
    {
        if (IsDisposed)
            return;

        ShouldRestoreFocusKey = CardDialogFocusKey;
        StateHasChanged();
    }

    private async Task<TModel> LoadFullEntityAsync(TModel item)
    {
        var idProperty = typeof(TModel).GetProperty("Id");
        var id = idProperty?.GetValue(item);

        if (id is null)
            return item;

        return await ResolvedDataProvider.GetByIdAsync(id) ?? item;
    }

    private async Task OnDeleteWithConfirmationAsync(TModel item)
    {
        var message = string.Format(FrameworkLocalizer["ConfirmDeleteNamed"].Value, ResolveItemDisplayName(item));

        if (!await ConfirmDeletionAsync(message))
            return;

        await OnDeleteItemAsync(item);
    }

    private async Task OnDeleteSelectedAsync()
    {
        var itemsToDelete = SelectedItems.ToList();

        var message = string.Format(FrameworkLocalizer["ConfirmDeleteMany"].Value, itemsToDelete.Count);

        if (!await ConfirmDeletionAsync(message))
            return;

        foreach (var item in itemsToDelete)
            await OnDeleteItemAsync(item);

        SelectionVersion++;
        SelectedItems.Clear();
    }

    private async Task<bool> ConfirmDeletionAsync(string message)
    {
        var fw = FrameworkLocalizer;
        var content = new ConfirmationDialogContent
        {
            Message = message,
            ConfirmButtonText = fw["Delete"].Value,
            CancelButtonText = fw["Cancel"].Value,
            IsDestructive = true
        };

        var dialog = await DialogService.ShowConfirmationDialogAsync(fw["ConfirmDeletionTitle"].Value, content);
        var result = await dialog.Result;

        ShouldRestoreFocusKey = ContextMenuFocusKey;
        StateHasChanged();

        return !result.Cancelled;
    }

    /// <summary>
    /// The property names that identify an item to a person: the card's display keys, then the entity's
    /// <see cref="Attributes.DisplayKeyAttribute"/> properties. Empty when the model declares neither.
    /// </summary>
    private List<string> ResolveExplicitDisplayKeyNames()
    {
        var cardKeys = new List<DisplayKeyFieldConfig<TModel>>();

        if (CardConfiguration is not null)
        {
            cardKeys.AddRange(CardConfiguration.DisplayKeys);
            cardKeys.AddRange(CardConfiguration.Fields
                .Where(field => field.IsDisplayKey)
                .Select(field => new DisplayKeyFieldConfig<TModel> { PropertyName = field.PropertyName, Order = field.DisplayKeyOrder }));
        }

        if (cardKeys.Count > 0)
            return [.. cardKeys.OrderBy(key => key.Order).Select(key => key.PropertyName)];

        return [.. DisplayKeyResolver.GetAttributeDisplayKeyPropertyNames(typeof(TModel))];
    }

    /// <summary>
    /// The name a confirmation uses for an item: its display key, otherwise the first value the row shows,
    /// otherwise its primary key. Naming the row is what lets a person tell whether they are about to delete
    /// the right one.
    /// </summary>
    private string ResolveItemDisplayName(TModel item)
    {
        var separator = CardConfiguration?.DisplayKeySeparator ?? DisplayKeyResolver.DefaultSeparator;

        var explicitName = DisplayKeyResolver.BuildDisplayString(item, ResolveExplicitDisplayKeyNames(), separator);
        if (!string.IsNullOrWhiteSpace(explicitName))
            return explicitName;

        foreach (var column in VisibleColumnsForUser)
        {
            var text = ResolveCellText(column, item);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return GetItemId(item)?.ToString() ?? string.Empty;
    }

    private string? DeleteErrorMessage;

    /// <summary>
    /// Deletes one row. A rule that refuses the delete, such as a category that is still in use, is shown above the list
    /// in the list's own language instead of surfacing as an unhandled error.
    /// </summary>
    private async Task OnDeleteItemAsync(TModel item)
    {
        var eventArgs = new ItemEventArgs<TModel> { Item = item };

        if (OnBeforeItemDelete.HasDelegate)
        {
            await OnBeforeItemDelete.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        var idProperty = typeof(TModel).GetProperty("Id");

        if (idProperty is null)
            return;

        var id = idProperty.GetValue(item);

        if (id is null)
            return;

        try
        {
            await ResolvedDataProvider.DeleteAsync(id);
        }
        catch (BaseValidationException ex)
        {
            DeleteErrorMessage = ResolvedLocalizer[ex.Message].Value;
            return;
        }

        DeleteErrorMessage = null;

        if (OnAfterItemDeleted.HasDelegate)
            await OnAfterItemDeleted.InvokeAsync(item);

        await LoadDataAsync(keepLoadedCount: true);
    }

    #endregion

    #region Deep Linking

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
        => _ = InvokeAsync(SyncDeepLinkAsync);

    /// <summary>
    /// Reconciles the open card with the URL: opens the item named by the query parameter, or closes the open
    /// card when the parameter is gone. This is the single place that opens or closes the deep-linked dialog.
    /// </summary>
    /// <remarks>
    /// The key is claimed before the first await. Opening is asynchronous (the item is fetched first), and a
    /// navigation can notify twice before that finishes - once for the click that navigated and once for the
    /// list the router recreates for the new URL. If the claim waited for the fetch, both would pass the
    /// "already open" check and stack two identical dialogs. Each claim also takes a ticket, so an open that
    /// was overtaken by a later navigation knows to stop instead of showing a stale card.
    /// </remarks>
    private async Task SyncDeepLinkAsync()
    {
        if (IsDisposed)
            return;

        var urlKey = QueryStringReader.TryReadValue(Navigation.Uri, DeepLinkParameterName);
        var key = string.IsNullOrEmpty(urlKey) ? null : urlKey;

        if (string.Equals(key, CurrentOpenKey, StringComparison.Ordinal))
            return;

        var ticket = ++DeepLinkTicket;
        CurrentOpenKey = key;

        if (OpenItemDialog is not null)
            await CloseOpenItemDialogAsync();

        if (key is not null)
            await OpenItemFromKeyAsync(key, ticket);
    }

    private bool IsCurrentDeepLink(int ticket) => !IsDisposed && ticket == DeepLinkTicket;

    private async Task OpenItemFromKeyAsync(string key, int ticket)
    {
        if (!CanEdit || ConvertKey(key) is not { } id)
        {
            AbandonDeepLink(ticket);
            return;
        }

        var entity = await ResolvedDataProvider.GetByIdAsync(id);

        if (!IsCurrentDeepLink(ticket))
            return;

        if (entity is null)
        {
            AbandonDeepLink(ticket);
            return;
        }

        var eventArgs = new ItemEventArgs<TModel> { Item = entity };

        if (OnBeforeItemUpdate.HasDelegate)
        {
            await OnBeforeItemUpdate.InvokeAsync(eventArgs);

            if (!IsCurrentDeepLink(ticket))
                return;

            if (eventArgs.Cancel)
            {
                AbandonDeepLink(ticket);
                return;
            }
        }

        var dialogData = new BaseDialogData<TModel>
        {
            Model = entity,
            DataProvider = ResolvedDataProvider,
            IsNew = false,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialog = await DialogService.ShowDialogAsync<BaseDialog<TModel>>(dialogData, BuildCardDialogParameters());

        if (!IsCurrentDeepLink(ticket))
        {
            await dialog.CloseAsync();
            return;
        }

        OpenItemDialog = dialog;
        ArmNavigationGuard(dialogData.CloseGuard);
        var dialogResult = await dialog.Result;

        if (ReferenceEquals(OpenItemDialog, dialog))
        {
            OpenItemDialog = null;
            DisarmNavigationGuard();
        }

        if (IsCurrentDeepLink(ticket))
        {
            CurrentOpenKey = null;
            CompleteClosedItemNavigation();
            RestoreCardDialogFocus();
        }
        else
        {
            PendingNavigationTarget = null;
        }

        if (dialogResult is { Cancelled: false, Data: TModel updatedItem })
        {
            if (OnAfterItemUpdated.HasDelegate)
                await OnAfterItemUpdated.InvokeAsync(updatedItem);

            if (!IsDisposed)
                await RefreshEditedItemAsync(updatedItem);
        }
    }

    /// <summary>
    /// Gives up on opening the item the URL names and removes the parameter, so the same link can be tried
    /// again instead of being mistaken for an already open card.
    /// </summary>
    private void AbandonDeepLink(int ticket)
    {
        if (!IsCurrentDeepLink(ticket))
            return;

        CurrentOpenKey = null;
        StripDeepLinkParameter();
    }

    /// <summary>
    /// Closes the open card because the URL moved on. The claim on the key was already replaced, so the
    /// call awaiting that card knows its close was not the user's and leaves the URL alone.
    /// </summary>
    private async Task CloseOpenItemDialogAsync()
    {
        var dialog = OpenItemDialog;
        OpenItemDialog = null;
        DisarmNavigationGuard();

        if (dialog is not null)
            await dialog.CloseAsync();
    }

    private void StripDeepLinkParameter()
    {
        var cleaned = Navigation.GetUriWithQueryParameter(DeepLinkParameterName, (string?)null);

        if (!string.Equals(cleaned, Navigation.Uri, StringComparison.Ordinal))
            Navigation.NavigateTo(cleaned, replace: true);
    }
    private static object? GetItemId(TModel item)
        => typeof(TModel).GetProperty("Id")?.GetValue(item);

    private static object? ConvertKey(string key)
    {
        var idProperty = typeof(TModel).GetProperty("Id");

        if (idProperty is null)
            return null;

        var targetType = Nullable.GetUnderlyingType(idProperty.PropertyType) ?? idProperty.PropertyType;

        if (targetType == typeof(string))
            return key;

        try
        {
            var converter = TypeDescriptor.GetConverter(targetType);

            if (converter.CanConvertFrom(typeof(string)))
                return converter.ConvertFromInvariantString(key);
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// The shared shape of every card dialog.
    /// </summary>
    /// <remarks>
    /// FluentUI's own focus trap stays off. A card can raise a second modal dialog on top of itself (the discard
    /// prompt), and two traps each pull focus back into their own dialog and fight over it. The card's
    /// <see cref="BaseDialog{TModel}"/> instead makes everything outside itself inert, which holds focus inside
    /// without any cooperation between the dialogs.
    /// </remarks>
    private static DialogParameters BuildCardDialogParameters() => new()
    {
        Title = string.Empty,
        ShowTitle = false,
        PrimaryAction = string.Empty,
        SecondaryAction = string.Empty,
        Width = "min(600px, calc(100vw - 2rem))",
        PreventDismissOnOverlayClick = true,
        ShowDismiss = false,
        TrapFocus = false
    };

    #endregion

    #region Action Helpers

    private ClaimsPrincipal? CurrentUser { get; set; }

    private bool HasToolbarActions =>
        ResolvedActionGroups.Any(g => g.Contexts.HasFlag(CrudActionContext.ListToolbar));

    private List<CrudActionGroup<TModel>> ContextMenuActionGroups =>
        ResolvedActionGroups
            .Where(g => g.Contexts.HasFlag(CrudActionContext.ContextMenu))
            .Where(g => IsGroupVisibleInContextMenu(g))
            .OrderBy(g => g.Order)
            .ToList();

    private bool IsGroupVisibleInContextMenu(CrudActionGroup<TModel> group)
    {
        if (group.VisibleForRoles is not null && CurrentUser is not null && !group.VisibleForRoles(CurrentUser))
            return false;

        return group.Actions.Any(a => a.Contexts.HasFlag(CrudActionContext.ContextMenu) && IsActionVisibleInContextMenu(a));
    }

    private bool IsActionVisibleInContextMenu(CrudAction<TModel> action)
    {
        if (!action.Contexts.HasFlag(CrudActionContext.ContextMenu))
            return false;

        if (action.VisibleForRoles is not null && CurrentUser is not null && !action.VisibleForRoles(CurrentUser))
            return false;

        return true;
    }

    private sealed record ContextMenuActionEntry(CrudAction<TModel> Action, CrudActionState<TModel> State, string? Reason);

    private sealed record ContextMenuActionSection(CrudActionGroup<TModel> Group, List<ContextMenuActionEntry> Entries);

    private List<ContextMenuActionSection> ContextMenuSections { get; set; } = [];

    /// <summary>
    /// Works out which declared actions the row menu offers for the clicked row, whether each is enabled and why not.
    /// Done once when the menu opens, so the async <c>Visible</c> predicates run for the clicked row only.
    /// </summary>
    private async Task BuildContextMenuSectionsAsync()
    {
        var sections = new List<ContextMenuActionSection>();
        var item = ContextMenuItem;

        if (item is not null)
        {
            IReadOnlyList<TModel> clicked = [item];

            foreach (var group in ResolvedActionGroups.OrderBy(g => g.Order))
            {
                if (!await CrudActionEvaluator<TModel>.IsGroupVisibleAsync(group, CrudActionContext.ContextMenu, CurrentUser, item, clicked))
                    continue;

                var entries = new List<ContextMenuActionEntry>();

                foreach (var action in group.Actions.OrderBy(a => a.Order))
                {
                    var targets = CrudActionEvaluator<TModel>.ResolveRowMenuTargets(action, item, SelectedItems, IsSelected);

                    if (!await CrudActionEvaluator<TModel>.IsVisibleAsync(action, CrudActionContext.ContextMenu, CurrentUser, item, targets))
                        continue;

                    var state = CrudActionEvaluator<TModel>.Evaluate(action, targets);
                    var reason = CrudActionEvaluator<TModel>.DescribeBlock(action, state, key => FrameworkLocalizer[key].Value, text => ResolveActionText(text));

                    entries.Add(new ContextMenuActionEntry(action, state, reason));
                }

                if (entries.Count > 0)
                    sections.Add(new ContextMenuActionSection(group, entries));
            }
        }

        ContextMenuSections = sections;
    }

    private string ResolveActionText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Localizer is not null ? Localizer[text]?.Value ?? text : text;
    }

    private async Task OnContextMenuActionClickedAsync(ContextMenuActionEntry entry)
    {
        var item = ContextMenuItem;
        CloseContextMenu();

        if (entry.Action.Action is null || !entry.State.IsEnabled)
            return;

        var eventArgs = CrudActionEvaluator<TModel>.BuildEventArgs(
            entry.Action, entry.State.Targets, item, ServiceProvider, CrudActionContext.ContextMenu);

        await entry.Action.Action(eventArgs);
        await RefreshAsync();
    }

    private async Task LoadCurrentUserAsync()
    {
        var authStateProvider = ServiceProvider.GetService<AuthenticationStateProvider>();
        if (authStateProvider is null)
            return;

        var authState = await authStateProvider.GetAuthenticationStateAsync();
        CurrentUser = authState.User;
    }

    #endregion

    #region Column Helpers

    private IStringLocalizer ResolvedLocalizer =>
        LocalizerResolver.ResolveProperty(ServiceProvider, Localizer, typeof(TModel));

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    private string ResolveColumnTitle(PropertyColumnConfig<TModel> column)
    {
        if (column.Title is not null)
            return column.Title;

        var result = ResolvedLocalizer[column.PropertyName];
        if (!result.ResourceNotFound)
            return result.Value;

        return column.PropertyName;
    }

    /// <summary>
    /// The name of a column's sort button: the title, followed by the direction when the user sorted by this column.
    /// </summary>
    /// <remarks>
    /// The grid already states the sort on the column header, but a screen reader announces a button by its name, so
    /// the direction is part of the name as well ("Name, sorted ascending").
    /// </remarks>
    private string ResolveColumnHeaderName(PropertyColumnConfig<TModel> column)
    {
        var title = ResolveColumnTitle(column);
        var sort = column.Sortable ? CurrentSorts.FirstOrDefault(current => current.PropertyName == column.PropertyName) : null;

        if (sort is null)
            return title;

        var key = sort.Direction == Models.SortDirection.Descending ? "ColumnSortedDescending" : "ColumnSortedAscending";

        return string.Format(FrameworkLocalizer[key].Value, title);
    }

    private string? ResolveColumnTooltip(PropertyColumnConfig<TModel> column)
    {
        if (column.Tooltip is not null)
            return column.Tooltip;

        return LocalizerResolver.ResolveOptional(ResolvedLocalizer, $"{column.PropertyName}_Tooltip");
    }

    /// <summary>
    /// The title of a column header, with the column's description as a tooltip on the text only.
    /// </summary>
    /// <remarks>
    /// Handing the description to the grid as the header tooltip also made it the accessible name of the sort
    /// button, so a screen reader announced "When this record was first created" instead of the column. The
    /// button keeps the title as its name and the sort state stays on the column header; the description is
    /// reachable as a tooltip. The text wraps to two lines before it is cut, and the cut text still has its
    /// full wording in the tooltip.
    /// </remarks>
    private RenderFragment<ColumnBase<TModel>> HeaderTitleTemplate(PropertyColumnConfig<TModel> column)
    {
        var title = ResolveColumnTitle(column);
        var tooltip = ResolveColumnTooltip(column);

        return _ => builder =>
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "base-list-header-title");
            builder.AddAttribute(2, "title", tooltip);
            builder.AddContent(3, title);
            builder.CloseElement();
        };
    }

    private GridSort<TModel>? CreateSortBy(PropertyColumnConfig<TModel> column)
    {
        if (!SortByColumns.TryGetValue(column, out var sortBy))
        {
            sortBy = GridSort<TModel>.ByAscending(column.Property);
            SortByColumns[column] = sortBy;
        }

        return sortBy;
    }

    private static string? FormatColumnValue(PropertyColumnConfig<TModel> column, TModel item)
    {
        var value = column.GetValue(item);

        if (value is null)
            return null;

        var property = ExpressionHelper.GetPropertyInfo(column.Property);

        if (BaseListDateTimeDisplay.IsDateTimeType(property.PropertyType))
            return BaseListDateTimeDisplay.Format(BaseListDateTimeDisplay.ToDisplayValue(property, value)!, column.Format, CultureInfo.CurrentCulture);

        return value is IFormattable formattable
            ? formattable.ToString(column.Format, CultureInfo.CurrentCulture)
            : value.ToString();
    }

    private static bool IsDateTimeColumn(PropertyColumnConfig<TModel> column) =>
        BaseListDateTimeDisplay.IsDateTimeType(ExpressionHelper.GetPropertyInfo(column.Property).PropertyType);

    private static bool IsBooleanColumn(PropertyColumnConfig<TModel> column)
    {
        var propertyType = ExpressionHelper.GetPropertyInfo(column.Property).PropertyType;

        return (Nullable.GetUnderlyingType(propertyType) ?? propertyType) == typeof(bool);
    }

    /// <summary>
    /// Reads a boolean cell the way the filter panel already offers it to the user.
    /// </summary>
    /// <remarks>
    /// Left to FluentUI's <c>PropertyColumn</c> the cell printed the raw CLR value, so a list showed
    /// "True" while its own filter for the same property offered "Yes" — and in a localized app the
    /// column stayed English.
    /// </remarks>
    private string? BooleanColumnText(PropertyColumnConfig<TModel> column, TModel item)
    {
        if (column.GetValue(item) is not bool value)
            return null;

        return value ? FrameworkLocalizer["BoolTrue"].Value : FrameworkLocalizer["BoolFalse"].Value;
    }

    private const string SelectionColumnWidth = "40px";
    private const string ActionsColumnWidth = "48px";

    /// <summary>
    /// The grid track of a column without an explicit width: a share of the free space with a floor that
    /// suits the data the column holds, see <see cref="BaseListColumnWidth"/>.
    /// </summary>
    /// <remarks>
    /// A bare <c>1fr</c> track has a content-based minimum, so a single long e-mail address widened its
    /// column to its own length and pushed the others out of view - and, with a virtualized grid, only when
    /// that row scrolled into view, so the columns jumped while scrolling. A fixed minimum makes the track
    /// independent of what the rows hold; the cell clips what does not fit with an ellipsis and carries
    /// the full text as a tooltip.
    /// </remarks>
    private string ResolveFlexibleColumnWidth(PropertyColumnConfig<TModel> column) =>
        BaseListColumnWidth.Resolve(ExpressionHelper.GetPropertyInfo(column.Property), ResolveColumnTitle(column));

    /// <summary>
    /// The CSS grid track for a column: what the caller asked for, otherwise a flexible share.
    /// </summary>
    /// <remarks>
    /// <see cref="PropertyColumnConfig{TModel}.Width"/> was never handed to FluentUI, so both the
    /// markup <c>Width</c> and the fluent <c>.Width(...)</c> silently did nothing. A track is emitted
    /// for every column because FluentUI builds the whole template from them.
    /// </remarks>
    private string ResolveColumnWidth(PropertyColumnConfig<TModel> column) => column.Width ?? ResolveFlexibleColumnWidth(column);

    /// <summary>
    /// Marks every data cell so the stylesheet can clip long values, and the columns without an explicit
    /// width so it can give them a readable minimum.
    /// </summary>
    private static string ResolveColumnClass(PropertyColumnConfig<TModel> column)
        => column.Width is null ? "base-list-cell base-list-column-flexible" : "base-list-cell";

    /// <summary>
    /// The text a cell shows for a value, or null when the value is not plain text (a template or a custom
    /// display draws it instead). Used for the tooltip that reveals what an ellipsis cut off.
    /// </summary>
    private string? ResolveCellText(PropertyColumnConfig<TModel> column, TModel item)
    {
        if (column.Template is not null || ResolveCustomDisplay(column) is not null)
            return null;

        if (column.Format is not null || IsDateTimeColumn(column))
            return FormatColumnValue(column, item);

        if (IsBooleanColumn(column))
            return BooleanColumnText(column, item);

        var value = column.GetValue(item);

        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.CurrentCulture)
            : value?.ToString();
    }
    #endregion

    #region Custom Display Resolution

    private static readonly Type NoCustomDisplaySentinel = typeof(NoCustomDisplayMarker);

    private sealed class NoCustomDisplayMarker;

    private Type? ResolveCustomDisplay(PropertyColumnConfig<TModel> column)
    {
        var propertyInfo = ExpressionHelper.GetPropertyInfo(column.Property);
        var cacheKey = (typeof(TModel), propertyInfo.MetadataToken);

        var resolved = CustomPropertyResolutionCache.Displays.GetOrAdd(cacheKey, _ => ResolveCustomDisplayType(propertyInfo));

        return ReferenceEquals(resolved, NoCustomDisplaySentinel) ? null : resolved;
    }

    private Type ResolveCustomDisplayType(System.Reflection.PropertyInfo propertyInfo)
    {
        var propertyType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
        var context = new CustomPropertyContext(typeof(TModel), propertyInfo, propertyType, IsEditing: false);

        foreach (var candidate in ServiceProvider.GetServices<IBaseCustomPropertyDisplay>())
        {
            if (candidate.CanHandle(context))
                return candidate.GetType();
        }

        return NoCustomDisplaySentinel;
    }

    private RenderFragment RenderCustomDisplayCell(Type displayType, PropertyColumnConfig<TModel> column, TModel item)
    {
        return builder =>
        {
            var propertyInfo = ExpressionHelper.GetPropertyInfo(column.Property);
            var parameters = new Dictionary<string, object?>
            {
                [nameof(IBaseCustomPropertyDisplay.Model)] = item,
                [nameof(IBaseCustomPropertyDisplay.Property)] = propertyInfo,
                [nameof(IBaseCustomPropertyDisplay.Value)] = propertyInfo.GetValue(item),
                [nameof(IBaseCustomPropertyDisplay.Localizer)] = ResolvedLocalizer
            };

            builder.OpenComponent<DynamicComponent>(0);
            builder.AddComponentParameter(1, nameof(DynamicComponent.Type), displayType);
            builder.AddComponentParameter(2, nameof(DynamicComponent.Parameters), parameters);
            builder.CloseComponent();
        };
    }

    #endregion

    /// <summary>
    /// Releases the list. A card it opened does not outlive it: the dialog service is shared, so a card left
    /// behind by a list that was replaced (the router rebuilds the page for a new URL) would stack with the one
    /// the replacement opens for the same item.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        DeepLinkTicket++;
        CurrentOpenKey = null;

        if (DeepLinkSubscribed)
            Navigation.LocationChanged -= OnLocationChanged;

        DisarmNavigationGuard();

        var dialog = OpenItemDialog;
        OpenItemDialog = null;

        if (dialog is not null)
            await dialog.CloseAsync();

        SelfReference?.Dispose();

        await DisposeMobileLayoutAsync();

        if (JsModule is null)
            return;

        try
        {
            await JsModule.InvokeVoidAsync("disposeGrid", GridContainerRef);
            await JsModule.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }
}
