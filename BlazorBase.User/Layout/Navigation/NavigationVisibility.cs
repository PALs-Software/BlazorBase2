using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.User.Layout.Navigation;

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

    private static bool IsAllowed(NavigationItem item, ClaimsPrincipal? user)
    {
        if (item.RequiredRole is null)
            return true;

        if (user is null)
            return false;

        foreach (var role in item.RequiredRole.Split(','))
        {
            var trimmed = role.Trim();
            if (trimmed.Length > 0 && user.IsInRole(trimmed))
                return true;
        }

        return false;
    }
}
