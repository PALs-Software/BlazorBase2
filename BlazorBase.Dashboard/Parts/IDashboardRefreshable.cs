namespace BlazorBase.Dashboard;

/// <summary>
/// Implemented by a part that can reload its data without being recreated. The role center calls it for the refresh
/// button, the silent auto refresh and <see cref="IDashboardRefreshService"/>.
/// </summary>
public interface IDashboardRefreshable
{
    /// <summary>
    /// Reloads the part's data. Should keep showing the old data while it runs; an exception shows the retry card of the part.
    /// </summary>
    Task RefreshAsync();
}
