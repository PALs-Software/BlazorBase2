namespace BlazorBase.Dashboard;

/// <summary>
/// The colour tokens of the current light or dark theme, resolved to concrete colours because a canvas cannot use CSS
/// variables. Handed to the chart fragment of a <c>ChartPart</c>; the part renders again when the theme changes.
/// </summary>
public sealed class ChartPalette
{
    /// <summary>
    /// Used before the theme could be read: every colour is <c>currentColor</c>, which a canvas resolves to its own text colour.
    /// </summary>
    public static ChartPalette Fallback { get; } = new();

    /// <summary>Primary text colour (<c>--color-text-primary</c>).</summary>
    public string Text { get; set; } = "currentColor";

    /// <summary>Secondary text colour for axis labels and legends (<c>--color-text-secondary</c>).</summary>
    public string TextSecondary { get; set; } = "currentColor";

    /// <summary>Grid line colour (<c>--color-border</c>).</summary>
    public string Grid { get; set; } = "currentColor";

    /// <summary>Brand accent (<c>--color-accent</c>); the default series colour.</summary>
    public string Accent { get; set; } = "currentColor";

    /// <summary>The accent mixed with the surface, for projections and secondary series.</summary>
    public string AccentMuted { get; set; } = "currentColor";

    /// <summary>Favorable status colour (<c>--color-success</c>).</summary>
    public string Success { get; set; } = "currentColor";

    /// <summary>Ambiguous status colour (<c>--color-warning</c>).</summary>
    public string Warning { get; set; } = "currentColor";

    /// <summary>Unfavorable status colour (<c>--color-danger</c>).</summary>
    public string Danger { get; set; } = "currentColor";

    /// <summary>Neutral colour for subordinate series (<c>--color-text-muted</c>).</summary>
    public string Neutral { get; set; } = "currentColor";
}
