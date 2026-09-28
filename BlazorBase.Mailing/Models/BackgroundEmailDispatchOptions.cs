namespace BlazorBase.Mailing.Models;

/// <summary>
/// Configuration for <see cref="EmailDispatchMode.Background"/> dispatch, bound from the host's
/// configuration like <see cref="SmtpSettings"/>.
/// </summary>
public class BackgroundEmailDispatchOptions
{
    /// <summary>
    /// Maximum number of messages the background queue holds before enqueueing blocks. Bounded
    /// rather than unbounded so a stuck or slow transport cannot exhaust process memory by
    /// letting the queue grow without limit.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;
}
