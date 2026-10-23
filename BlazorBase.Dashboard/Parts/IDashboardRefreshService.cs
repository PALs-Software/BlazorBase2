namespace BlazorBase.Dashboard;

/// <summary>
/// Cascaded by the role center: any part can ask for a refresh of all or of named parts, for example after an inline
/// decision changed the numbers of other parts.
/// </summary>
public interface IDashboardRefreshService
{
    /// <summary>
    /// Refreshes every part that implements <see cref="IDashboardRefreshable"/> and re-evaluates which parts are visible.
    /// A call while a refresh is running joins that refresh.
    /// </summary>
    Task RefreshAllAsync();

    /// <summary>
    /// Refreshes only the parts with the given ids (see <see cref="DashboardPartRegistration.Id"/>); unknown ids are ignored.
    /// </summary>
    Task RefreshAsync(params string[] partIds);

    /// <summary>
    /// Includes a component that is not a registered part, such as the headline, in the refresh. Dispose the result to
    /// leave the refresh again.
    /// </summary>
    IDisposable Register(string id, IDashboardRefreshable refreshable);
}
