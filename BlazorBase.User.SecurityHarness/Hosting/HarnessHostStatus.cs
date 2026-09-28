namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>How a <see cref="HarnessProfile"/>'s host came out of <see cref="HarnessHostFactory"/>.</summary>
public enum HarnessHostStatus
{
    /// <summary>The host is up and reachable at <see cref="HarnessHostResult.BaseAddress"/>.</summary>
    Started,

    /// <summary>The host build or the first request into it failed - see <see cref="HarnessHostResult.FailureMessage"/>.</summary>
    FailedToStart,

    /// <summary>The profile was not attempted at all because a prerequisite it needs was not supplied.</summary>
    Unavailable,
}
