using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Decides whether a <see cref="NavigationItem"/> points at the page currently shown.
/// </summary>
/// <remarks>
/// The rules are <see cref="NavLink"/>'s own, deliberately down to the trailing-slash and separator
/// details: the link renders its active styling from <see cref="NavLink"/> while
/// <c>aria-current</c> comes from here, and a screen reader announcing a different tab as the current
/// page than the one highlighted is worse than announcing none at all.
/// </remarks>
internal static class NavigationActivation
{
    /// <remarks>
    /// An empty <see cref="NavigationItem.Href"/> is a route, not a missing one: it is how Blazor spells
    /// the application root, and <c>ToAbsoluteUri</c> resolves it to the base URI. Only a null href means
    /// the entry does not navigate at all, which is what a group or an action is.
    /// </remarks>
    public static bool IsActive(NavigationManager navigationManager, NavigationItem item)
    {
        if (item.Href is null)
            return false;

        var href = navigationManager.ToAbsoluteUri(item.Href).AbsoluteUri;
        var current = navigationManager.Uri;

        if (EqualsHrefExactlyOrIfTrailingSlashAdded(current, href))
            return true;

        return !item.MatchAll && IsStrictlyPrefixWithSeparator(current, href);
    }

    public static string? AriaCurrent(NavigationManager navigationManager, NavigationItem item)
    {
        return IsActive(navigationManager, item) ? "page" : null;
    }

    private static bool EqualsHrefExactlyOrIfTrailingSlashAdded(string currentUriAbsolute, string hrefAbsolute)
    {
        if (string.Equals(currentUriAbsolute, hrefAbsolute, StringComparison.OrdinalIgnoreCase))
            return true;

        if (currentUriAbsolute.Length == hrefAbsolute.Length - 1
            && hrefAbsolute[^1] == '/'
            && hrefAbsolute.StartsWith(currentUriAbsolute, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static bool IsStrictlyPrefixWithSeparator(string value, string prefix)
    {
        if (value.Length <= prefix.Length)
            return false;

        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        return prefix.Length == 0
            || !char.IsLetterOrDigit(prefix[^1])
            || !char.IsLetterOrDigit(value[prefix.Length]);
    }
}
