using System.Security.Claims;
using BlazorBase.Components.Layout.Navigation;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.Components.Layout.AppSwitcher;

/// <summary>
/// Filters app switcher groups down to the links the current user may see, with the same role semantics as
/// <see cref="NavigationVisibility"/>. Groups left without links are dropped.
/// </summary>
internal static class AppLinkVisibility
{
    public static async Task<IReadOnlyList<AppLinkGroup>> FilterAsync(
        IReadOnlyList<AppLinkGroup>? groups,
        Task<AuthenticationState>? authenticationState)
    {
        if (groups is null || groups.Count == 0)
            return [];

        var user = authenticationState is null ? null : (await authenticationState).User;
        return Filter(groups, user);
    }

    private static List<AppLinkGroup> Filter(IReadOnlyList<AppLinkGroup> groups, ClaimsPrincipal? user)
    {
        var result = new List<AppLinkGroup>();
        foreach (var group in groups)
        {
            if (!NavigationVisibility.IsRoleAllowed(group.RequiredRole, user))
                continue;

            var visibleLinks = group.Links.Where(link => NavigationVisibility.IsRoleAllowed(link.RequiredRole, user)).ToList();
            if (visibleLinks.Count == 0)
                continue;

            result.Add(group with { Links = visibleLinks });
        }

        return result;
    }
}
