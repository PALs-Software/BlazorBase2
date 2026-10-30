namespace BlazorBase.Dashboard;

/// <summary>
/// Persists the layout a user arranged for a dashboard. Implementations always act for the current user and never expose
/// another user's layout. Register one with <c>AddBlazorBaseDashboardLayoutsClient</c>, <c>AddBlazorBaseDashboardLayoutsLocalStorage</c>
/// or <c>AddBlazorBaseDashboardLayoutsInMemory</c>; without a store the role center offers no personalization.
/// </summary>
public interface IDashboardLayoutStore
{
    /// <summary>
    /// Returns the current user's layout for the dashboard, or an empty (default) layout when the user never arranged it.
    /// </summary>
    Task<DashboardLayout> GetAsync(string dashboardKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the layout, replacing a previous one.
    /// </summary>
    /// <exception cref="DashboardLayoutValidationException">The key or the layout is not acceptable.</exception>
    Task SaveAsync(string dashboardKey, DashboardLayout layout, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the stored layout so the dashboard returns to its default arrangement. Resetting a dashboard without a stored
    /// layout is not an error.
    /// </summary>
    Task ResetAsync(string dashboardKey, CancellationToken cancellationToken = default);
}
