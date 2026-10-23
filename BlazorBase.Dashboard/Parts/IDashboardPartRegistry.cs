namespace BlazorBase.Dashboard;

/// <summary>
/// The catalogue of parts a <c>RoleCenter</c> can show. Modules add their parts at start-up through
/// <c>AddBlazorBaseDashboard</c>; the role center reads the ordered list.
/// </summary>
public interface IDashboardPartRegistry
{
    /// <summary>
    /// A snapshot of all registered parts ordered by <see cref="DashboardPartRegistration.Priority"/>, then by registration order.
    /// </summary>
    IReadOnlyList<DashboardPartRegistration> Parts { get; }

    /// <summary>
    /// Adds a part. Throws <see cref="InvalidOperationException"/> when the id is already taken or the type is not a component.
    /// </summary>
    void Register(DashboardPartRegistration registration);
}
