using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    #endregion

    #region Mobile State

    private IJSObjectReference? MobileJsModule { get; set; }
    private bool IsNarrowContainer { get; set; }

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
        if (MobileLayout != BaseListMobileLayout.Auto || EffectiveMobileBreakpoint <= 0)
            return;

        MobileJsModule = await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.CRUD/js/baseListMobile.js");

        if (IsDisposed)
        {
            await DisposeMobileLayoutAsync();
            return;
        }

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

    private BaseListCardViewModel<TModel> BuildMobileView()
    {
        var card = EffectiveMobileCard;
        var sort = CurrentSorts.FirstOrDefault();

        return new BaseListCardViewModel<TModel>
        {
            Items = LoadedItems,
            Plan = MobilePlan,
            Localizer = FrameworkLocalizer,
            Template = MobileTemplate ?? card?.Template,
            Actions = card?.Actions,
            IsClickable = CanEdit || OnItemSelected.HasDelegate,
            SelectionActive = IsSelectionActive,
            IsSelected = IsSelected,
            OnOpen = RerenderAfterAsync<TModel>(OnMobileItemOpenedAsync),
            OnToggleSelect = item =>
            {
                ToggleSelection(item);
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
    /// What tapping a tile does: the same as clicking a row, except that while a selection is going on it adds
    /// the tile to the selection, because there is no modifier key on a touch screen.
    /// </summary>
    private async Task OnMobileItemOpenedAsync(TModel item)
    {
        if (IsSelectionActive)
        {
            ToggleSelection(item);
            return;
        }

        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(item);

        if (CanEdit)
            await OnEditClickedAsync(item);
    }

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
    /// The text of a badge: the column title for a boolean that is true, nothing for one that is false, the
    /// displayed value for anything else.
    /// </summary>
    private string? ResolveMobileBadgeText(PropertyColumnConfig<TModel> column, TModel item)
    {
        if (IsBooleanColumn(column))
            return column.GetValue(item) is true ? ResolveColumnTitle(column) : null;

        return ResolveCellText(column, item);
    }
}
