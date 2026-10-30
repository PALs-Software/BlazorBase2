namespace BlazorBase.Dashboard;

/// <summary>
/// Applies a user's <see cref="DashboardLayout"/> to the parts the user may see. Role rules are decided before this runs: only
/// parts that passed them are handed in, so a layout can reorder and hide but never reveal a part.
/// </summary>
public static class DashboardLayoutArranger
{
    /// <summary>
    /// Orders the parts and marks the hidden ones.
    /// </summary>
    /// <param name="visibleParts">The parts the user may see, in default order (by priority).</param>
    /// <param name="layout">The stored layout, or null for the default arrangement.</param>
    /// <remarks>
    /// Parts the layout mentions come first in the layout's order. Ids in the layout that are not in <paramref name="visibleParts"/>
    /// are ignored. Parts the layout does not know (added since it was saved) are inserted right after the part that precedes them
    /// in the default order, or at the start when nothing precedes them.
    /// </remarks>
    public static IReadOnlyList<DashboardArrangedPart> Arrange(IReadOnlyList<DashboardPartRegistration> visibleParts, DashboardLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(visibleParts);

        var preferences = BuildPreferences(layout);
        var defaultPosition = visibleParts.Select((part, index) => (part, index)).ToDictionary(item => item.part.Id, item => item.index, StringComparer.Ordinal);

        var ordered = visibleParts
            .Where(part => preferences.ContainsKey(part.Id))
            .OrderBy(part => preferences[part.Id].Order)
            .ThenBy(part => defaultPosition[part.Id])
            .ToList();

        foreach (var part in visibleParts.Where(part => !preferences.ContainsKey(part.Id)))
            ordered.Insert(InsertionIndex(visibleParts, ordered, defaultPosition[part.Id]), part);

        return [.. ordered.Select(part => new DashboardArrangedPart(part, preferences.TryGetValue(part.Id, out var preference) && preference.Hidden))];
    }

    /// <summary>
    /// Builds the layout that stores the given arrangement: one preference per part, ordered from 0.
    /// </summary>
    public static DashboardLayout ToLayout(IEnumerable<DashboardArrangedPart> arrangement)
    {
        ArgumentNullException.ThrowIfNull(arrangement);

        return new DashboardLayout
        {
            Parts = [.. arrangement.Select((part, index) => new DashboardPartPreference { PartId = part.Registration.Id, Hidden = part.Hidden, Order = index })]
        };
    }

    private static Dictionary<string, DashboardPartPreference> BuildPreferences(DashboardLayout? layout)
    {
        var preferences = new Dictionary<string, DashboardPartPreference>(StringComparer.Ordinal);

        foreach (var preference in layout?.Parts ?? [])
            if (preference is not null && !String.IsNullOrEmpty(preference.PartId))
                preferences.TryAdd(preference.PartId, preference);

        return preferences;
    }

    private static int InsertionIndex(IReadOnlyList<DashboardPartRegistration> visibleParts, List<DashboardPartRegistration> ordered, int position)
    {
        for (var index = position - 1; index >= 0; index--)
        {
            var predecessor = ordered.FindIndex(part => ReferenceEquals(part, visibleParts[index]));

            if (predecessor >= 0)
                return predecessor + 1;
        }

        return 0;
    }
}
