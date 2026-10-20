using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Renders a <see cref="NavigationItem"/> list as a side navigation (Fluent UI nav menu). Inside a
/// <see cref="BaseLayout"/> it is the persistent rail on wide screens — reducible to icons — and the
/// content of the off-canvas drawer on narrow ones.
/// Groups render as a labelled section with their children listed beneath; role-gated entries are
/// hidden for users that lack the role. Pair it with <see cref="BaseBottomNavigation"/> driven by the
/// same list so desktop and mobile cannot drift apart.
/// </summary>
public partial class BaseSideNavigation : ComponentBase
{
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

    private IReadOnlyList<NavigationItem> VisibleItems = [];

    private bool IsIconOnly => IsCollapsed || LayoutContext?.IsCollapsed == true;

    protected override async Task OnParametersSetAsync()
    {
        VisibleItems = await NavigationVisibility.FilterAsync(Items, AuthenticationState);
    }

    private Task CloseDrawerAsync() => LayoutContext?.CloseDrawerAsync() ?? Task.CompletedTask;

    private async Task InvokeItemAsync(NavigationItem item)
    {
        await CloseDrawerAsync();

        if (item.OnClick is null)
            return;

        await item.OnClick();
    }
}
