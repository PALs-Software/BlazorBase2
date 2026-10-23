namespace BlazorBase.Dashboard;

/// <summary>
/// The settings of the registered dashboard that components read at run time.
/// </summary>
public sealed class DashboardSettings
{
    /// <summary>
    /// The automatic refresh interval when nothing else is configured.
    /// </summary>
    public static readonly TimeSpan DefaultAutoRefreshInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often a visible role center reloads its parts silently; zero switches it off.
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = DefaultAutoRefreshInterval;

    /// <summary>
    /// The longest a role center waits for the stored layout before it shows the default arrangement. Zero waits without limit.
    /// </summary>
    public TimeSpan LayoutLoadTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
