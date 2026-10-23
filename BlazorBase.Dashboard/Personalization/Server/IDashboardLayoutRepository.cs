namespace BlazorBase.Dashboard.Server;

/// <summary>
/// Server-side storage of dashboard layouts. Every operation is scoped to a user identifier: an implementation must never
/// return, change or delete a row that belongs to another user.
/// </summary>
public interface IDashboardLayoutRepository
{
    /// <summary>
    /// Returns the user's layout for the dashboard, or an empty layout when there is none.
    /// </summary>
    Task<DashboardLayout> GetAsync(string userId, string dashboardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the user's layout for the dashboard.
    /// </summary>
    /// <exception cref="DashboardLayoutValidationException">The key or the layout is not acceptable.</exception>
    Task SaveAsync(string userId, string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default);

    /// <returns>False when the user had no stored layout.</returns>
    Task<bool> DeleteAsync(string userId, string dashboardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every layout of a user across all dashboards, for hosts whose model has no cascading foreign key to the user table.
    /// </summary>
    /// <returns>The number of deleted layouts.</returns>
    Task<int> DeleteAllForUserAsync(string userId, CancellationToken cancellationToken = default);
}
