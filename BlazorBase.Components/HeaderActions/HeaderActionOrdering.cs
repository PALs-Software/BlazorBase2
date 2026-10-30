using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace BlazorBase.Components.HeaderActions;

/// <summary>Merging, ordering and access filtering of header actions.</summary>
internal static class HeaderActionOrdering
{
    public static IReadOnlyList<HeaderActionItem> Order(IEnumerable<HeaderActionItem> items) =>
        items.OrderBy(item => item.Order).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();

    public static IReadOnlyList<HeaderActionItem> Merge(IEnumerable<HeaderActionItem> registered, IEnumerable<HeaderActionItem>? additional)
    {
        var byId = new Dictionary<string, HeaderActionItem>(StringComparer.Ordinal);
        foreach (var item in registered.Concat(additional ?? []))
            byId[item.Id] = item;

        return Order(byId.Values);
    }

    public static async Task<IReadOnlyList<HeaderActionItem>> FilterAsync(
        IReadOnlyList<HeaderActionItem> items,
        IServiceProvider services,
        ClaimsPrincipal? user,
        IAuthorizationService? authorization)
    {
        var visible = new List<HeaderActionItem>(items.Count);
        foreach (var item in items)
        {
            if (item.IsVisible?.Invoke(services) == false)
                continue;

            if (!await IsAllowedAsync(item, user, authorization))
                continue;

            visible.Add(item);
        }

        return visible;
    }

    private static async Task<bool> IsAllowedAsync(HeaderActionItem item, ClaimsPrincipal? user, IAuthorizationService? authorization)
    {
        var needsRole = !string.IsNullOrWhiteSpace(item.RequiredRole);
        var needsPolicy = !string.IsNullOrWhiteSpace(item.RequiredPolicy);
        if (!needsRole && !needsPolicy)
            return true;

        if (user?.Identity?.IsAuthenticated != true)
            return false;

        if (needsRole)
        {
            var roles = item.RequiredRole!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!roles.Any(user.IsInRole))
                return false;
        }

        if (needsPolicy)
        {
            if (authorization is null)
                return false;

            var result = await authorization.AuthorizeAsync(user, item.RequiredPolicy!);
            if (!result.Succeeded)
                return false;
        }

        return true;
    }
}
