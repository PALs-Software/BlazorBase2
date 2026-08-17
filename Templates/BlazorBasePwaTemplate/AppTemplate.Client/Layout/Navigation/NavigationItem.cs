using Microsoft.AspNetCore.Components.Routing;

namespace AppTemplate.Client.Layout.Navigation;

/// <summary>
/// One entry of the main navigation, rendered both in the desktop rail and the mobile bottom bar.
/// </summary>
/// <param name="Label">The visible caption.</param>
/// <param name="Href">The target route, relative to the base path.</param>
/// <param name="Match">How the active state is determined.</param>
public record NavigationItem(string Label, string Href, NavLinkMatch Match = NavLinkMatch.Prefix);
