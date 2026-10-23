namespace BlazorBase.Dashboard;

/// <summary>
/// Ready-made visibility rules for <see cref="DashboardPartRegistration.IsVisibleAsync"/>.
/// </summary>
public static class DashboardVisibility
{
    /// <summary>
    /// Visible when the current user is in at least one of <paramref name="roles"/>. Without any role nobody sees the part.
    /// </summary>
    public static Func<DashboardPartContext, ValueTask<bool>> ForRoles(params string[] roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        var wanted = roles.Where(role => !String.IsNullOrWhiteSpace(role)).ToArray();

        return context => ValueTask.FromResult(wanted.Any(context.User.IsInRole));
    }

    /// <summary>
    /// Visible to every user the role center is shown to.
    /// </summary>
    public static Func<DashboardPartContext, ValueTask<bool>> ForAnyUser() => static _ => ValueTask.FromResult(true);

    /// <summary>
    /// Visible when the synchronous <paramref name="predicate"/> holds for the user.
    /// </summary>
    public static Func<DashboardPartContext, ValueTask<bool>> ForUsers(Func<System.Security.Claims.ClaimsPrincipal, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return context => ValueTask.FromResult(predicate(context.User));
    }
}
