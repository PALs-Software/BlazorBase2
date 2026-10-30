namespace BlazorBase.Dashboard;

/// <summary>
/// Which end of the scale of a cue value is the bad one.
/// </summary>
public enum CueDirection
{
    /// <summary>A high value is bad (budget used, failed jobs): the value rises into Ambiguous, then Unfavorable.</summary>
    HigherIsWorse,

    /// <summary>A low value is bad (stock, coverage): the value falls into Ambiguous, then Unfavorable.</summary>
    HigherIsBetter,
}
