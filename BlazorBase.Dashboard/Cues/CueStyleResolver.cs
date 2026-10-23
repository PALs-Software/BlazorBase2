namespace BlazorBase.Dashboard;

/// <summary>
/// Turns a cue's number into its traffic-light style.
/// </summary>
public static class CueStyleResolver
{
    /// <summary>
    /// Resolves <paramref name="value"/> against <paramref name="thresholds"/>: Favorable, Ambiguous or Unfavorable.
    /// Both boundaries are inclusive, and Unfavorable wins when both apply.
    /// </summary>
    public static CueStyle Resolve(decimal value, CueThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);

        var rising = thresholds.Direction == CueDirection.HigherIsWorse;

        if (rising ? value >= thresholds.UnfavorableFrom : value <= thresholds.UnfavorableFrom)
            return CueStyle.Unfavorable;

        if (rising ? value >= thresholds.AmbiguousFrom : value <= thresholds.AmbiguousFrom)
            return CueStyle.Ambiguous;

        return CueStyle.Favorable;
    }

    /// <summary>
    /// The style of a count that only matters when it is above zero: <paramref name="whenPositive"/> for one or more,
    /// <paramref name="whenZero"/> (no status by default) for none.
    /// </summary>
    public static CueStyle ForCount(decimal count, CueStyle whenPositive, CueStyle whenZero = CueStyle.None) =>
        count > 0 ? whenPositive : whenZero;
}
