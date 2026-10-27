namespace BlazorBase.Components.Layout.AppSwitcher;

/// <summary>
/// The app links and the current app that <see cref="BaseLayout"/> cascades to its header, so a
/// <see cref="BaseAppSwitcher"/> inside <see cref="BaseLayout.AppSwitcher"/> needs no parameters of its own.
/// </summary>
/// <param name="Groups">The value of <see cref="BaseLayout.AppLinks"/>.</param>
/// <param name="CurrentKey">The value of <see cref="BaseLayout.CurrentAppKey"/>.</param>
public sealed record AppSwitcherContext(IReadOnlyList<AppLinkGroup> Groups, string? CurrentKey);
