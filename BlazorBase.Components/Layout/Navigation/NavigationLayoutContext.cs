namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// What <see cref="BaseLayout"/> tells the navigation placed inside it: whether the desktop rail is
/// reduced to icons and how to dismiss the off-canvas drawer after an entry was activated.
/// </summary>
/// <param name="IsCollapsed">True when the persistent side navigation shows icons only.</param>
/// <param name="CloseDrawerAsync">Closes the off-canvas drawer; does nothing when it is not open.</param>
/// <param name="BottomBarMaxItems">
/// The most tabs the bottom bar shows while it is on screen, otherwise null. The side navigation then lists the
/// <see cref="NavigationItem.BottomBarOnly"/> entries that did not become a tab, because the drawer is the
/// bar's "More".
/// </param>
public sealed record NavigationLayoutContext(bool IsCollapsed, Func<Task> CloseDrawerAsync, int? BottomBarMaxItems = null);
