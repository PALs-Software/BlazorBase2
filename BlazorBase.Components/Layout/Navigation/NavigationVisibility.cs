using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Filters a navigation tree down to the entries the current user may see, evaluating
/// <see cref="NavigationItem.RequiredRole"/> against the authenticated principal. Groups whose
/// children are all hidden are dropped as well. Shared by the side and bottom navigation renderers
/// so role visibility behaves identically on every form factor.
/// </summary>
internal static class NavigationVisibility
{
    public static async Task<IReadOnlyList<NavigationItem>> FilterAsync(
        IReadOnlyList<NavigationItem> items,
        Task<AuthenticationState>? authenticationState)
    {
        var user = authenticationState is null ? null : (await authenticationState).User;
        return Filter(items, user);
    }

    /// <summary>
    /// Removes the <see cref="NavigationItem.BottomBarOnly"/> entries from a list the user may see. With
    /// <paramref name="selectedTabs"/> set (the bottom bar is on screen and this list is its "More"), only the
    /// entries that are one of those tabs (compared by reference) are removed and the others stay. Groups left
    /// without children are dropped.
    /// </summary>
    public static IReadOnlyList<NavigationItem> WithoutBottomBarOnly(
        IReadOnlyList<NavigationItem> items,
        IReadOnlyCollection<NavigationItem>? selectedTabs = null)
    {
        var result = new List<NavigationItem>();
        foreach (var item in items)
        {
            if (item.BottomBarOnly && (selectedTabs is null || selectedTabs.Contains(item, ReferenceEqualityComparer.Instance)))
                continue;

            if (!item.IsGroup)
            {
                result.Add(item);
                continue;
            }

            var remainingChildren = WithoutBottomBarOnly(item.Children, selectedTabs);
            if (remainingChildren.Count == 0)
                continue;

            result.Add(item with { Children = remainingChildren });
        }

        return result;
    }

    private static IReadOnlyList<NavigationItem> Filter(IReadOnlyList<NavigationItem> items, ClaimsPrincipal? user)
    {
        var result = new List<NavigationItem>();
        foreach (var item in items)
        {
            if (!IsAllowed(item, user))
                continue;

            if (!item.IsGroup)
            {
                result.Add(item);
                continue;
            }

            var visibleChildren = Filter(item.Children, user);
            if (visibleChildren.Count == 0)
                continue;

            result.Add(item with { Children = visibleChildren });
        }

        return result;
    }

    private static bool IsAllowed(NavigationItem item, ClaimsPrincipal? user) => IsRoleAllowed(item.RequiredRole, user);

    /// <summary>
    /// True when <paramref name="requiredRole"/> is null or the user is in at least one of its comma separated roles.
    /// </summary>
    internal static bool IsRoleAllowed(string? requiredRole, ClaimsPrincipal? user)
    {
        if (requiredRole is null)
            return true;

        if (user is null)
            return false;

        foreach (var role in requiredRole.Split(','))
        {
            var trimmed = role.Trim();
            if (trimmed.Length > 0 && user.IsInRole(trimmed))
                return true;
        }

        return false;
    }
}
