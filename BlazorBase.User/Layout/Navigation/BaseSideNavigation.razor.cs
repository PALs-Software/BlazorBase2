using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.User.Layout.Navigation;

/// <summary>
/// Renders a <see cref="NavigationItem"/> list as a desktop side navigation (Fluent UI nav menu).
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

    private IReadOnlyList<NavigationItem> VisibleItems = [];

    protected override async Task OnParametersSetAsync()
    {
        VisibleItems = await NavigationVisibility.FilterAsync(Items, AuthenticationState);
    }

    private static async Task InvokeItemAsync(NavigationItem item)
    {
        if (item.OnClick is null)
            return;

        await item.OnClick();
    }
}
