using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Renders a <see cref="NavigationItem"/> list as a mobile bottom tab bar. Entries flagged
/// <see cref="NavigationItem.IsMobilePrimary"/> become bottom tabs; everything else (secondary links,
/// groups and actions) moves into a slide-up "More" sheet. Groups drill down to their children with a
/// back step. Drive it from the same list as <see cref="BaseSideNavigation"/> so both stay in sync.
/// </summary>
public partial class BaseBottomNavigation(
    IStringLocalizer<BaseBottomNavigation> localizer,
    NavigationManager navigationManager) : ComponentBase, IDisposable
{
    #region Injects

    private readonly IStringLocalizer<BaseBottomNavigation> Localizer = localizer;
    private readonly NavigationManager NavigationManager = navigationManager;

    #endregion

    [Parameter]
    public IReadOnlyList<NavigationItem> Items { get; set; } = [];

    /// <summary>
    /// Optional host-supplied content rendered at the top of the "More" sheet root view (above the
    /// secondary navigation list). Lets a host place richer controls — a tenant/household switcher,
    /// a theme toggle, a user summary — into the sheet instead of the top header on small screens.
    /// </summary>
    [Parameter]
    public RenderFragment? MoreSheetContent { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    private IReadOnlyList<NavigationItem> VisibleItems = [];
    private IReadOnlyList<NavigationItem> PrimaryItems = [];
    private IReadOnlyList<NavigationItem> MoreItems = [];

    private bool IsMoreOpen;
    private NavigationItem? OpenGroup;
    private bool ShouldFocusSheet;
    private bool ReturnFocus;
    private ElementReference SheetElement;
    private ElementReference MoreButtonElement;

    private static readonly Icon MoreIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size24.MoreHorizontal();
    private static readonly Icon ChevronRightIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ChevronRight();
    private static readonly Icon ChevronLeftIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ChevronLeft();

    private bool HasMore => MoreItems.Count > 0 || MoreSheetContent is not null;

    /// <summary>
    /// Marks the tab or sheet entry that leads to the page currently shown.
    /// </summary>
    /// <remarks>
    /// <c>NavLink</c> only ever sets its active CSS class, which is invisible to a screen reader —
    /// without <c>aria-current</c> the bar announces five equal links and never says where the user is.
    /// The state has to be recomputed on navigation, hence the subscription below.
    /// </remarks>
    private string? AriaCurrent(NavigationItem item) => NavigationActivation.AriaCurrent(NavigationManager, item);

    protected override void OnInitialized()
    {
        NavigationManager.LocationChanged += OnLocationChanged;
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) => InvokeAsync(StateHasChanged);

    /// <summary>
    /// Rebuilds the two lists and keeps the opened group open across a re-render.
    /// </summary>
    /// <remarks>
    /// The group is matched by label rather than by the item. <see cref="NavigationItem"/> is a record,
    /// but the filter rebuilds every group as <c>item with { Children = … }</c> around a fresh list, and
    /// a list carries no value equality — so comparing the items themselves never matched and the sheet
    /// jumped back to its root list on any parent re-render, a token refresh being enough to trigger it.
    /// Re-reading the filtered instance also keeps the rendered children current.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        VisibleItems = await NavigationVisibility.FilterAsync(Items, AuthenticationState);
        PrimaryItems = VisibleItems.Where(item => item.IsMobilePrimary && !item.IsGroup && !item.IsAction).ToList();
        MoreItems = VisibleItems.Where(item => !PrimaryItems.Contains(item)).ToList();

        if (OpenGroup is null)
            return;

        var stillPresent = MoreItems.FirstOrDefault(item => item.IsGroup && item.Label == OpenGroup.Label);
        OpenGroup = stillPresent;
    }

    private void ToggleMoreSheet()
    {
        IsMoreOpen = !IsMoreOpen;
        OpenGroup = null;
        ShouldFocusSheet = IsMoreOpen;
    }

    private void CloseMoreSheet()
    {
        IsMoreOpen = false;
        OpenGroup = null;
        ReturnFocus = true;
    }

    /// <summary>
    /// Escape closes the sheet, the way every other modal surface behaves.
    /// </summary>
    private void OnSheetKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            CloseMoreSheet();
    }

    /// <summary>
    /// Moves focus into the sheet when it opens and back onto the button that opened it when it closes.
    /// </summary>
    /// <remarks>
    /// The sheet announces itself as <c>aria-modal</c>, which tells assistive technology the rest of the
    /// page is inert. Without moving focus that promise is false and worse than saying nothing: the
    /// reader is told to stay, while the keyboard walks straight out of the sheet with no way back.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (ShouldFocusSheet)
        {
            ShouldFocusSheet = false;
            await SheetElement.FocusAsync();
            return;
        }

        if (!ReturnFocus)
            return;

        ReturnFocus = false;

        if (HasMore)
            await MoreButtonElement.FocusAsync();
    }

    private void OpenGroupView(NavigationItem group)
    {
        OpenGroup = group;
    }

    private void BackToRoot()
    {
        OpenGroup = null;
    }

    private async Task InvokeActionAsync(NavigationItem item)
    {
        CloseMoreSheet();

        if (item.OnClick is null)
            return;

        await item.OnClick();
    }

    public void Dispose()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;
    }
}
