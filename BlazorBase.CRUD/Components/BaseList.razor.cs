using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using BlazorBase.CRUD.Components.CustomProperties;
using BlazorBase.CRUD.Components.Filtering;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
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
    private List<TModel> LoadedItems { get; set; } = [];
    private List<FilterDescriptor> CurrentFilters { get; set; } = [];
    private List<SortDescriptor> CurrentSorts { get; set; } = [];
    private int TotalCount { get; set; }
    private bool IsLoading { get; set; }
    private bool HasMoreItems => LoadedItems.Count < TotalCount;

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

    private HashSet<TModel> SelectedItems { get; } = [];
    private bool IsSelectionActive => SelectedItems.Count > 0;

    #endregion

    #region Context Menu State

    private TModel? ContextMenuItem { get; set; }
    private double ContextMenuX { get; set; }
    private double ContextMenuY { get; set; }
    private bool ContextMenuOpen { get; set; }

    #endregion

    #region Interaction State

    private bool LastShiftState { get; set; }
    private ElementReference GridContainerRef { get; set; }
    private FluentDataGrid<TModel>? DataGrid { get; set; }
    private IQueryable<TModel>? GridQuery { get; set; }
    private List<TModel>? GridQuerySource { get; set; }
    private int DataVersion { get; set; }
    private int RenderedVersion { get; set; }
    private int RefreshedVersion { get; set; }
    private IJSObjectReference? JsModule { get; set; }

    #endregion

    #region Deep Linking State

    private string? CurrentOpenKey { get; set; }
    private IDialogReference? OpenItemDialog { get; set; }
    private bool DeepLinkSubscribed { get; set; }

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
            return;
        }

        JsModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/baseListInterop.js");

        await LoadCurrentUserAsync();
        ResolveFilterFields();
        await LoadDataAsync();

        if (EnableDeepLinking)
        {
            Navigation.LocationChanged += OnLocationChanged;
            DeepLinkSubscribed = true;
            await SyncDeepLinkAsync();
        }
    }

    protected override async Task OnParametersSetAsync()
    {
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

        if (LastShiftState)
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
        if (JsModule is not null)
        {
            var isOnRow = await JsModule.InvokeAsync<bool>("isPointOnDataRow", GridContainerRef, e.ClientX, e.ClientY);
            if (!isOnRow)
                return;
        }

        ContextMenuX = e.ClientX;
        ContextMenuY = e.ClientY;
        ContextMenuOpen = true;
    }

    private void CloseContextMenu()
    {
        ContextMenuOpen = false;
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
        if (!SelectedItems.Remove(item))
            SelectedItems.Add(item);
    }

    private void SelectAll()
    {
        foreach (var item in LoadedItems)
            SelectedItems.Add(item);

        CloseContextMenu();
    }

    private void DeselectAll()
    {
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

        return [.. selectFields];
    }

    private async Task LoadDataAsync(bool reset = true)
    {
        IsLoading = true;
        StateHasChanged();

        var filters = new List<FilterDescriptor>(CurrentFilters);
        if (AdditionalFilters is not null)
            filters.AddRange(AdditionalFilters);

        var query = new BaseQuery
        {
            Filters = filters,
            Sorts = CurrentSorts,
            Select = BuildSelectList(),
            NavigationFilters = NavigationFilters,
            Skip = reset ? 0 : LoadedItems.Count,
            Take = 50
        };

        var result = await ResolvedDataProvider.GetListAsync(query);
        TotalCount = result.TotalCount;

        if (reset)
            LoadedItems = result.Items;
        else
            LoadedItems.AddRange(result.Items);

        IsLoading = false;
        DataVersion++;
        StateHasChanged();
    }

    /// <summary>
    /// Hands the grid a query that only changes when the data behind it does, and records which load
    /// this render carried.
    /// </summary>
    /// <remarks>
    /// <c>AsQueryable()</c> wraps a list in a new object every call, and the grid compares its
    /// <c>Items</c> by reference — so building it inline made every single render look like a new data
    /// source to the grid: it tore down and recreated a DI scope and re-queried, and the explicit
    /// refresh below then cancelled that and queried again. Caching the query per collection instance
    /// stops the churn; the version counter is what still tells a genuine reload apart, including
    /// loading a further page, which keeps the same collection and would otherwise go unnoticed.
    /// </remarks>
    private IQueryable<TModel> CaptureRenderedItems()
    {
        if (GridQuery is null || !ReferenceEquals(GridQuerySource, LoadedItems))
        {
            GridQuerySource = LoadedItems;
            GridQuery = LoadedItems.AsQueryable();
        }

        RenderedVersion = DataVersion;

        return GridQuery;
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

    public async Task LoadMoreAsync()
    {
        if (!HasMoreItems || IsLoading)
            return;

        await LoadDataAsync(reset: false);
    }

    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    #endregion

    #region Filtering

    private FilterConfig<TModel>? CollectedFilterConfigComponent { get; set; }
    private List<FilterFieldMetadata> FilterFields { get; set; } = [];
    private FilterGroupModel ActiveFilterModel { get; set; } = new();
    private FilterGroupModel? WorkingFilterModel { get; set; }
    private bool FilterPanelOpen { get; set; }

    private FilterConfiguration<TModel> ResolvedFilterConfig =>
        Configuration?.Filter ?? CollectedFilterConfigComponent?.BuildConfiguration() ?? new FilterConfiguration<TModel>();

    private bool IsFilteringEnabled => FilterFields.Count > 0;

    private int ActiveFilterCount => FilterModelConverter.CountConditions(ActiveFilterModel);

    private void ResolveFilterFields()
    {
        FilterFields = FilterFieldResolver.Resolve(ResolvedFilterConfig, CurrentUser, ResolvedLocalizer);
    }

    private void OpenFilterPanel()
    {
        WorkingFilterModel = ActiveFilterModel.Clone();
        FilterPanelOpen = true;
    }

    private void CloseFilterPanel()
    {
        FilterPanelOpen = false;
        WorkingFilterModel = null;
    }

    private async Task OnFilterApplyAsync(FilterGroupModel root)
    {
        ActiveFilterModel = root;

        var descriptor = FilterModelConverter.ToDescriptor(root);
        CurrentFilters = descriptor is null ? [] : [descriptor];

        FilterPanelOpen = false;
        WorkingFilterModel = null;
        await LoadDataAsync();
    }

    private async Task OnFilterResetAsync()
    {
        ActiveFilterModel = new FilterGroupModel();
        WorkingFilterModel = new FilterGroupModel();
        CurrentFilters = [];
        await LoadDataAsync();
    }

    #endregion

    #region CRUD Operations

    private async Task OnAddClickedAsync()
    {
        var newItem = Activator.CreateInstance<TModel>();

        var eventArgs = new ItemEventArgs<TModel> { Item = newItem };

        if (OnBeforeItemCreate.HasDelegate)
        {
            await OnBeforeItemCreate.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        var fw = FrameworkLocalizer;
        var dialogParameters = new DialogParameters
        {
            Title = string.Empty,
            ShowTitle = false,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            Width = "600px",
            PreventDismissOnOverlayClick = true,
            ShowDismiss = false,
            TrapFocus = false
        };

        var dialogData = new BaseDialogData<TModel>
        {
            Model = newItem,
            DataProvider = ResolvedDataProvider,
            IsNew = true,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialog = await DialogService.ShowDialogAsync<BaseDialog<TModel>>(dialogData, dialogParameters);
        var dialogResult = await dialog.Result;

        if (dialogResult is { Cancelled: false, Data: TModel savedItem })
        {
            if (OnAfterItemCreated.HasDelegate)
                await OnAfterItemCreated.InvokeAsync(savedItem);

            await LoadDataAsync();
        }
    }

    private async Task OnEditClickedAsync(TModel item)
    {
        if (EnableDeepLinking && GetItemId(item) is { } deepLinkId)
        {
            Navigation.NavigateTo(Navigation.GetUriWithQueryParameter(DeepLinkParameterName, deepLinkId.ToString()));
            return;
        }

        var eventArgs = new ItemEventArgs<TModel> { Item = item };

        if (OnBeforeItemUpdate.HasDelegate)
        {
            await OnBeforeItemUpdate.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
                return;
        }

        var editModel = await LoadFullEntityAsync(item);

        var dialogParameters = new DialogParameters
        {
            Title = string.Empty,
            ShowTitle = false,
            PrimaryAction = string.Empty,
            SecondaryAction = string.Empty,
            Width = "600px",
            PreventDismissOnOverlayClick = true,
            ShowDismiss = false,
            TrapFocus = false
        };

        var dialogData = new BaseDialogData<TModel>
        {
            Model = editModel,
            DataProvider = ResolvedDataProvider,
            IsNew = false,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        var dialog = await DialogService.ShowDialogAsync<BaseDialog<TModel>>(dialogData, dialogParameters);
        var dialogResult = await dialog.Result;

        if (dialogResult is { Cancelled: false, Data: TModel updatedItem })
        {
            if (OnAfterItemUpdated.HasDelegate)
                await OnAfterItemUpdated.InvokeAsync(updatedItem);

            await LoadDataAsync();
        }
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
        var fw = FrameworkLocalizer;
        var dialog = await DialogService.ShowConfirmationAsync(
            fw["ConfirmDeleteOne"].Value,
            fw["Delete"].Value,
            fw["Cancel"].Value,
            fw["ConfirmDeletionTitle"].Value);

        var result = await dialog.Result;
        if (result.Cancelled)
            return;

        await OnDeleteItemAsync(item);
    }

    private async Task OnDeleteSelectedAsync()
    {
        var itemsToDelete = SelectedItems.ToList();

        var fw = FrameworkLocalizer;
        var dialog = await DialogService.ShowConfirmationAsync(
            string.Format(fw["ConfirmDeleteMany"].Value, itemsToDelete.Count),
            fw["Delete"].Value,
            fw["Cancel"].Value,
            fw["ConfirmDeletionTitle"].Value);

        var result = await dialog.Result;
        if (result.Cancelled)
            return;

        foreach (var item in itemsToDelete)
            await OnDeleteItemAsync(item);

        SelectedItems.Clear();
    }

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

        await ResolvedDataProvider.DeleteAsync(id);

        if (OnAfterItemDeleted.HasDelegate)
            await OnAfterItemDeleted.InvokeAsync(item);

        await LoadDataAsync();
    }

    #endregion

    #region Deep Linking

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args)
        => _ = InvokeAsync(SyncDeepLinkAsync);

    /// <summary>
    /// Reconciles the open card with the URL: opens the item named by the query parameter, or closes the open
    /// card when the parameter is gone. This is the single place that opens or closes the deep-linked dialog.
    /// </summary>
    private async Task SyncDeepLinkAsync()
    {
        var urlKey = QueryStringReader.TryReadValue(Navigation.Uri, DeepLinkParameterName);

        if (string.Equals(urlKey, CurrentOpenKey, StringComparison.Ordinal))
            return;

        if (OpenItemDialog is not null)
            await CloseOpenItemDialogAsync();

        if (!string.IsNullOrEmpty(urlKey))
            await OpenItemFromKeyAsync(urlKey);
    }

    private async Task OpenItemFromKeyAsync(string key)
    {
        if (!CanEdit || ConvertKey(key) is not { } id)
        {
            StripDeepLinkParameter();
            return;
        }

        var entity = await ResolvedDataProvider.GetByIdAsync(id);

        if (entity is null)
        {
            StripDeepLinkParameter();
            return;
        }

        var eventArgs = new ItemEventArgs<TModel> { Item = entity };

        if (OnBeforeItemUpdate.HasDelegate)
        {
            await OnBeforeItemUpdate.InvokeAsync(eventArgs);

            if (eventArgs.Cancel)
            {
                StripDeepLinkParameter();
                return;
            }
        }

        CurrentOpenKey = key;

        var dialogData = new BaseDialogData<TModel>
        {
            Model = entity,
            DataProvider = ResolvedDataProvider,
            IsNew = false,
            Localizer = Localizer,
            CardConfiguration = CardConfiguration,
            CardType = CardType
        };

        OpenItemDialog = await DialogService.ShowDialogAsync<BaseDialog<TModel>>(dialogData, BuildCardDialogParameters());
        var dialogResult = await OpenItemDialog.Result;

        OpenItemDialog = null;

        // Still our key => the dialog was closed through the UI (not by a Back-navigation that already cleared it).
        if (string.Equals(CurrentOpenKey, key, StringComparison.Ordinal))
        {
            CurrentOpenKey = null;
            StripDeepLinkParameter();
        }

        if (dialogResult is { Cancelled: false, Data: TModel updatedItem })
        {
            if (OnAfterItemUpdated.HasDelegate)
                await OnAfterItemUpdated.InvokeAsync(updatedItem);

            await LoadDataAsync();
        }
    }

    /// <summary>Closes the open card in response to a Back-navigation. Clears state first so the awaiting open call does not strip the (already changed) URL.</summary>
    private async Task CloseOpenItemDialogAsync()
    {
        CurrentOpenKey = null;
        var dialog = OpenItemDialog;
        OpenItemDialog = null;

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

    private static DialogParameters BuildCardDialogParameters() => new()
    {
        Title = string.Empty,
        ShowTitle = false,
        PrimaryAction = string.Empty,
        SecondaryAction = string.Empty,
        Width = "600px",
        PreventDismissOnOverlayClick = true,
        ShowDismiss = false,
        TrapFocus = false
    };

    #endregion

    #region Action Helpers

    private ClaimsPrincipal? CurrentUser { get; set; }

    private bool HasToolbarActions =>
        ResolvedActionGroups.Any(g => g.Contexts.HasFlag(CrudActionContext.ListToolbar));

    private bool HasContextMenuActionGroups =>
        ResolvedActionGroups.Any(g => g.Contexts.HasFlag(CrudActionContext.ContextMenu));

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

    private async Task OnContextMenuActionClickedAsync(CrudAction<TModel> action)
    {
        var item = ContextMenuItem;
        CloseContextMenu();

        if (action.Action is null)
            return;

        var eventArgs = new CrudActionEventArgs<TModel>
        {
            Item = item,
            SelectedItems = action.IsBulkAction && IsSelectionActive ? SelectedItems.ToList() : (item is not null ? [item] : []),
            ServiceProvider = ServiceProvider,
            Context = CrudActionContext.ContextMenu
        };

        await action.Action(eventArgs);
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

    private string? ResolveColumnTooltip(PropertyColumnConfig<TModel> column)
    {
        if (column.Tooltip is not null)
            return column.Tooltip;

        return LocalizerResolver.ResolveOptional(ResolvedLocalizer, $"{column.PropertyName}_Tooltip");
    }

    private static GridSort<TModel>? CreateSortBy(PropertyColumnConfig<TModel> column)
    {
        var propertyInfo = ExpressionHelper.GetPropertyInfo(column.Property);
        return GridSort<TModel>.ByAscending(column.Property);
    }

    private static string? FormatColumnValue(PropertyColumnConfig<TModel> column, TModel item)
    {
        var value = column.GetValue(item);

        if (value is null)
            return null;

        return value is IFormattable formattable
            ? formattable.ToString(column.Format, CultureInfo.CurrentCulture)
            : value.ToString();
    }

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

    /// <summary>
    /// The CSS grid track for a column: what the caller asked for, otherwise an equal share.
    /// </summary>
    /// <remarks>
    /// <see cref="PropertyColumnConfig{TModel}.Width"/> was never handed to FluentUI, so both the
    /// markup <c>Width</c> and the fluent <c>.Width(...)</c> silently did nothing. A track is emitted
    /// for every column because FluentUI builds the whole template from them.
    /// </remarks>
    private static string ResolveColumnWidth(PropertyColumnConfig<TModel> column) => column.Width ?? "1fr";

    /// <summary>
    /// Marks columns that carry no explicit width, so the stylesheet can keep them readable.
    /// </summary>
    /// <remarks>
    /// An equal share of a narrow viewport is not enough to read an address or a name: four columns on
    /// a phone truncated every one of them to an ellipsis. The class carries a minimum width, which
    /// makes the grid scroll sideways instead — a column the caller sized explicitly is left alone.
    /// </remarks>
    private static string? ResolveColumnClass(PropertyColumnConfig<TModel> column)
        => column.Width is null ? "base-list-column-flexible" : null;

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

    public async ValueTask DisposeAsync()
    {
        if (DeepLinkSubscribed)
            Navigation.LocationChanged -= OnLocationChanged;

        if (JsModule is not null)
            await JsModule.DisposeAsync();
    }
}
