namespace BlazorBase.Components.Layout.Navigation;

/// <summary>
/// Chooses the quick links shown as tabs in the bottom bar from a navigation tree the user may already see.
/// </summary>
internal static class BottomBarSelector
{
    /// <summary>
    /// Entries flagged <see cref="NavigationItem.IsMobilePrimary"/> win, taken from anywhere in the tree and
    /// ordered by <see cref="NavigationItem.BottomBarOrder"/> (ties keep the list order). Without any flagged
    /// entry the first top-level links fill the bar so a host that configured nothing still gets a usable one.
    /// </summary>
    public static IReadOnlyList<NavigationItem> Select(IReadOnlyList<NavigationItem> visibleItems, int maxItems)
    {
        var flagged = Flatten(visibleItems)
            .Where(item => item.IsMobilePrimary && IsLink(item))
            .OrderBy(item => item.BottomBarOrder)
            .ToList();

        if (flagged.Count == 0)
            flagged = visibleItems.Where(IsLink).ToList();

        return flagged.Take(Math.Max(maxItems, 0)).ToList();
    }

    private static bool IsLink(NavigationItem item) => item.Href is not null && !item.IsGroup && !item.IsAction;

    private static IEnumerable<NavigationItem> Flatten(IEnumerable<NavigationItem> items)
    {
        foreach (var item in items)
        {
            yield return item;

            foreach (var child in Flatten(item.Children))
                yield return child;
        }
    }
}
