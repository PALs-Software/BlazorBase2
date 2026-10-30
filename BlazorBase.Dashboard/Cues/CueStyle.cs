namespace BlazorBase.Dashboard;

/// <summary>
/// The traffic-light style of a <c>Cue</c>, after Business Central cue styles.
/// </summary>
public enum CueStyle
{
    /// <summary>No status; no stripe, no status word.</summary>
    None = 0,

    /// <summary>Good news or on track (green stripe).</summary>
    Favorable,

    /// <summary>Worth a look (amber stripe).</summary>
    Ambiguous,

    /// <summary>Needs action (red stripe and red number).</summary>
    Unfavorable,

    /// <summary>Background information (grey stripe).</summary>
    Subordinate,
}
