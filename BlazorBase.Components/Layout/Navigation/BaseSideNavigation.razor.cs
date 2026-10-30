using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Renders a <see cref="NavigationItem"/> list as a side navigation (Fluent UI nav). Inside a
/// <see cref="BaseLayout"/> it is the persistent rail on wide screens — reducible to icons — and the
/// content of the off-canvas drawer on narrow ones.
/// Groups render as a collapsible nav category with the group label and a chevron, with the
/// children listed beneath. Each group remembers in the browser whether it is expanded; the group that holds
/// the page being shown is always expanded when the page is reached. In the icon-only rail a group is a
/// single icon button that shows or hides the icons of its children. Role-gated entries are
/// hidden for users that lack the role. Pair it with <see cref="BaseBottomNavigation"/> driven by the
/// same list so desktop and mobile cannot drift apart.
/// </summary>
public partial class BaseSideNavigation(
    IJSRuntime jsRuntime,
    NavigationManager navigationManager) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly NavigationManager NavigationManager = navigationManager;

    #endregion

    [Parameter]
    public IReadOnlyList<NavigationItem> Items { get; set; } = [];

    [Parameter]
    public bool IsCollapsed { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? AriaLabel { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthenticationState { get; set; }

    [CascadingParameter]
    private NavigationLayoutContext? LayoutContext { get; set; }

    private const int ExpandedWidth = 260;

    private readonly Dictionary<string, bool> GroupStates = [];

    private readonly HashSet<string> LoadedKeys = [];

    private IReadOnlyList<NavigationItem> VisibleItems = [];
    private IJSObjectReference? Module;

    private bool IsIconOnly => IsCollapsed || LayoutContext?.IsCollapsed == true;

    protected override void OnInitialized() => NavigationManager.LocationChanged += OnLocationChanged;

    protected override async Task OnParametersSetAsync()
    {
        var visible = await NavigationVisibility.FilterAsync(Items, AuthenticationState);
        var bottomBarMax = LayoutContext?.BottomBarMaxItems;
        VisibleItems = NavigationVisibility.WithoutBottomBarOnly(
            visible,
            bottomBarMax is null ? null : BottomBarSelector.Select(visible, bottomBarMax.Value));

        try
        {
            await LoadStoredStatesAsync();
        }
        catch (InvalidOperationException)
        {
        }

        ExpandGroupsHoldingTheCurrentPage();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !HasUnloadedGroups())
            return;

        await LoadStoredStatesAsync();
        ExpandGroupsHoldingTheCurrentPage();
        StateHasChanged();
    }

    private bool HasUnloadedGroups() => Items.Any(item => item.IsGroup && !LoadedKeys.Contains(item.StateKey));

    /// <summary>
    /// Loads the remembered state of every group not read yet, before it is rendered, so the navigation does not
    /// open or close groups after it was already shown. A host builds its items asynchronously, so groups can
    /// turn up after the first parameters; each key is read once and never overwrites a choice made meanwhile.
    /// Where JavaScript is not yet available (prerendering) the call throws and the first-render pass runs it instead.
    /// </summary>
    private async Task LoadStoredStatesAsync()
    {
        var keys = Items.Where(item => item.IsGroup && !LoadedKeys.Contains(item.StateKey))
            .Select(item => item.StateKey)
            .Distinct()
            .ToArray();

        if (keys.Length == 0)
            return;

        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/layout.js");

        var stored = await Module.InvokeAsync<Dictionary<string, bool>?>("readGroupStates", new object[] { keys });

        foreach (var key in keys)
        {
            if (stored is not null && stored.TryGetValue(key, out var expanded))
                GroupStates.TryAdd(key, expanded);

            LoadedKeys.Add(key);
        }
    }

    private bool IsExpanded(NavigationItem group) => GroupStates.GetValueOrDefault(group.StateKey, group.ExpandedByDefault);

    private bool ContainsActiveChild(NavigationItem group) =>
        group.Children.Any(child => NavigationActivation.IsActive(NavigationManager, child));

    private static string GroupItemsId(NavigationItem group) =>
        "base-nav-group-" + new string(group.StateKey.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());

    private void ExpandGroupsHoldingTheCurrentPage()
    {
        foreach (var group in VisibleItems.Where(item => item.IsGroup && ContainsActiveChild(item)))
            GroupStates[group.StateKey] = true;
    }

    private Task ToggleGroupAsync(NavigationItem group) => SetGroupExpandedAsync(group, !IsExpanded(group));

    private async Task SetGroupExpandedAsync(NavigationItem group, bool expanded)
    {
        GroupStates[group.StateKey] = expanded;

        if (Module is null)
            return;

        await Module.InvokeVoidAsync("writeGroupState", group.StateKey, expanded);
    }

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) =>
        _ = InvokeAsync(() =>
        {
            ExpandGroupsHoldingTheCurrentPage();
            StateHasChanged();
        });

    private Task CloseDrawerAsync() => LayoutContext?.CloseDrawerAsync() ?? Task.CompletedTask;

    private async Task InvokeItemAsync(NavigationItem item)
    {
        await CloseDrawerAsync();

        if (item.OnClick is null)
            return;

        await item.OnClick();
    }

    public async ValueTask DisposeAsync()
    {
        NavigationManager.LocationChanged -= OnLocationChanged;

        if (Module is not null)
        {
            try
            {
                await Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            Module = null;
        }

        GC.SuppressFinalize(this);
    }
}
