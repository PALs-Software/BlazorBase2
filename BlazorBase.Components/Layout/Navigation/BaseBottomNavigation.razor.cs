using Microsoft.AspNetCore.Components;
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
public partial class BaseBottomNavigation(IStringLocalizer<BaseBottomNavigation> localizer) : ComponentBase
{
    #region Injects

    private readonly IStringLocalizer<BaseBottomNavigation> Localizer = localizer;

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

    private static readonly Icon MoreIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size24.MoreHorizontal();
    private static readonly Icon ChevronRightIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ChevronRight();
    private static readonly Icon ChevronLeftIcon = new Microsoft.FluentUI.AspNetCore.Components.Icons.Regular.Size20.ChevronLeft();

    private bool HasMore => MoreItems.Count > 0 || MoreSheetContent is not null;

    protected override async Task OnParametersSetAsync()
    {
        VisibleItems = await NavigationVisibility.FilterAsync(Items, AuthenticationState);
        PrimaryItems = VisibleItems.Where(item => item.IsMobilePrimary && !item.IsGroup && !item.IsAction).ToList();
        MoreItems = VisibleItems.Where(item => !PrimaryItems.Contains(item)).ToList();

        if (OpenGroup is not null && !MoreItems.Contains(OpenGroup))
            OpenGroup = null;
    }

    private void ToggleMoreSheet()
    {
        IsMoreOpen = !IsMoreOpen;
        OpenGroup = null;
    }

    private void CloseMoreSheet()
    {
        IsMoreOpen = false;
        OpenGroup = null;
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
}
