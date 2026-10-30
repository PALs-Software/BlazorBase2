namespace BlazorBase.CRUD.Components;

/// <summary>
/// The screen rectangle a row menu opens from: a point for a right-click, the button's box for the
/// per-row more button. Coordinates are viewport (client) pixels.
/// </summary>
public readonly record struct ContextMenuAnchor(double Left, double Top, double Right, double Bottom)
{
    public static ContextMenuAnchor AtPoint(double x, double y) => new(x, y, x, y);
}

/// <summary>Where a row menu goes: its top-left corner, and a height cap when it cannot fit whole.</summary>
public readonly record struct ContextMenuPlacement(double Left, double Top, double? MaxHeight);

/// <summary>
/// Places a row menu inside the viewport.
/// </summary>
/// <remarks>
/// The menu is <c>position: fixed</c>, so what lies outside the viewport can never be scrolled into view. It
/// opens below and to the right of its anchor, flips above and to the left when that side lacks room, and is
/// clamped as the last resort. A menu taller than the viewport is capped and scrolls inside itself.
/// </remarks>
public static class BaseListContextMenuPlacement
{
    /// <summary>Space kept free between the menu and the viewport edge.</summary>
    public const double ViewportMargin = 8;

    public static ContextMenuPlacement Compute(ContextMenuAnchor anchor, double menuWidth, double menuHeight, double viewportWidth, double viewportHeight)
    {
        var left = ResolveLeft(anchor, menuWidth, viewportWidth);

        var availableHeight = viewportHeight - (2 * ViewportMargin);
        if (menuHeight > availableHeight)
            return new ContextMenuPlacement(left, ViewportMargin, Math.Max(availableHeight, 0));

        return new ContextMenuPlacement(left, ResolveTop(anchor, menuHeight, viewportHeight), null);
    }

    private static double ResolveLeft(ContextMenuAnchor anchor, double menuWidth, double viewportWidth)
    {
        var rightLimit = viewportWidth - ViewportMargin;
        var candidate = anchor.Left + menuWidth <= rightLimit ? anchor.Left : anchor.Right - menuWidth;

        return ClampInto(candidate, menuWidth, rightLimit);
    }

    private static double ResolveTop(ContextMenuAnchor anchor, double menuHeight, double viewportHeight)
    {
        var bottomLimit = viewportHeight - ViewportMargin;
        var candidate = anchor.Bottom + menuHeight <= bottomLimit ? anchor.Bottom : anchor.Top - menuHeight;

        return ClampInto(candidate, menuHeight, bottomLimit);
    }

    private static double ClampInto(double start, double size, double limit)
        => Math.Clamp(start, ViewportMargin, Math.Max(limit - size, ViewportMargin));
}