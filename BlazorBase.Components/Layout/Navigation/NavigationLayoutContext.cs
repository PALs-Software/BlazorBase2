namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// What <see cref="BaseLayout"/> tells the navigation placed inside it: whether the desktop rail is
/// reduced to icons and how to dismiss the off-canvas drawer after an entry was activated.
/// </summary>
/// <param name="IsCollapsed">True when the persistent side navigation shows icons only.</param>
/// <param name="CloseDrawerAsync">Closes the off-canvas drawer; does nothing when it is not open.</param>
public sealed record NavigationLayoutContext(bool IsCollapsed, Func<Task> CloseDrawerAsync);
