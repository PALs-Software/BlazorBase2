namespace BlazorBase.Dashboard;

/// <summary>
/// Limits applied to everything a user can store as a dashboard layout, so a client cannot persist unbounded data.
/// </summary>
public static class DashboardLayoutValidator
{
    /// <summary>
    /// The longest accepted dashboard key.
    /// </summary>
    public const int MaxKeyLength = 256;

    /// <summary>
    /// The most parts one layout may carry.
    /// </summary>
    public const int MaxParts = 64;

    /// <summary>
    /// The longest accepted part id.
    /// </summary>
    public const int MaxPartIdLength = 100;

    /// <summary>
    /// The highest accepted <see cref="DashboardPartPreference.Order"/>.
    /// </summary>
    public const int MaxOrder = 10_000;

    /// <summary>
    /// Rejects an empty or over-long dashboard key.
    /// </summary>
    public static string ValidateKey(string? dashboardKey)
    {
        if (String.IsNullOrWhiteSpace(dashboardKey))
            throw new DashboardLayoutValidationException("The dashboard key must not be empty.");

        if (dashboardKey.Length > MaxKeyLength)
            throw new DashboardLayoutValidationException($"The dashboard key must not be longer than {MaxKeyLength} characters.");

        return dashboardKey;
    }

    /// <summary>
    /// Rejects a layout with too many parts, empty, over-long or duplicate part ids, or an order outside the accepted range.
    /// A missing part list is treated as an empty layout.
    /// </summary>
    public static void ValidateLayout(DashboardLayout? layout)
    {
        if (layout is null)
            throw new DashboardLayoutValidationException("The layout must not be empty.");

        var parts = layout.Parts ?? [];

        if (parts.Count > MaxParts)
            throw new DashboardLayoutValidationException($"A layout must not have more than {MaxParts} parts.");

        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var part in parts)
        {
            if (part is null || String.IsNullOrWhiteSpace(part.PartId))
                throw new DashboardLayoutValidationException("A part id must not be empty.");

            if (part.PartId.Length > MaxPartIdLength)
                throw new DashboardLayoutValidationException($"A part id must not be longer than {MaxPartIdLength} characters.");

            if (part.Order < 0 || part.Order > MaxOrder)
                throw new DashboardLayoutValidationException($"The order of a part must be between 0 and {MaxOrder}.");

            if (!seen.Add(part.PartId))
                throw new DashboardLayoutValidationException($"The part '{part.PartId}' appears more than once.");
        }
    }

    /// <summary>
    /// Returns a validated copy of the layout with the parts sorted by order, ready to be stored.
    /// </summary>
    public static DashboardLayout Normalize(DashboardLayout? layout)
    {
        ValidateLayout(layout);

        return new DashboardLayout
        {
            Parts = [.. (layout!.Parts ?? []).OrderBy(part => part.Order).Select(part => new DashboardPartPreference { PartId = part.PartId, Hidden = part.Hidden, Order = part.Order })]
        };
    }
}
