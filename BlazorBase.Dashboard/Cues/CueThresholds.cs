namespace BlazorBase.Dashboard;

/// <summary>
/// The two boundaries that turn a number into a <see cref="CueStyle"/>; see <see cref="CueStyleResolver"/>.
/// </summary>
public sealed record CueThresholds
{
    private CueThresholds(CueDirection direction, decimal ambiguousFrom, decimal unfavorableFrom)
    {
        Direction = direction;
        AmbiguousFrom = ambiguousFrom;
        UnfavorableFrom = unfavorableFrom;
    }

    /// <summary>
    /// Which end of the scale is bad.
    /// </summary>
    public CueDirection Direction { get; }

    /// <summary>
    /// The boundary of Ambiguous, inclusive: from this value on (upwards for HigherIsWorse, downwards for HigherIsBetter).
    /// </summary>
    public decimal AmbiguousFrom { get; }

    /// <summary>
    /// The boundary of Unfavorable, inclusive, in the same direction as <see cref="AmbiguousFrom"/>.
    /// </summary>
    public decimal UnfavorableFrom { get; }

    /// <summary>
    /// A rising value gets worse: <c>HigherIsWorse(75, 90)</c> is Favorable below 75, Ambiguous from 75 up to just below 90,
    /// Unfavorable from 90. Throws when <paramref name="unfavorableFrom"/> is below <paramref name="ambiguousFrom"/>.
    /// </summary>
    public static CueThresholds HigherIsWorse(decimal ambiguousFrom, decimal unfavorableFrom)
    {
        if (unfavorableFrom < ambiguousFrom)
            throw new ArgumentException("For HigherIsWorse the unfavorable boundary must not be below the ambiguous one.", nameof(unfavorableFrom));

        return new CueThresholds(CueDirection.HigherIsWorse, ambiguousFrom, unfavorableFrom);
    }

    /// <summary>
    /// A falling value gets worse: <c>HigherIsBetter(50, 20)</c> is Favorable above 50, Ambiguous from 50 down to just above 20,
    /// Unfavorable from 20 down. Throws when <paramref name="unfavorableFrom"/> is above <paramref name="ambiguousFrom"/>.
    /// </summary>
    public static CueThresholds HigherIsBetter(decimal ambiguousFrom, decimal unfavorableFrom)
    {
        if (unfavorableFrom > ambiguousFrom)
            throw new ArgumentException("For HigherIsBetter the unfavorable boundary must not be above the ambiguous one.", nameof(unfavorableFrom));

        return new CueThresholds(CueDirection.HigherIsBetter, ambiguousFrom, unfavorableFrom);
    }
}
