namespace BlazorBase.User.Services;

/// <summary>
/// App-specific seam supplying the role names selectable in the user role input. Roles are not
/// generic, so the consuming app registers an implementation; the framework only consumes it. The
/// returned names are presented as-is in the role select of the user CRUD card.
/// </summary>
public interface IUserRoleProvider
{
    /// <summary>Returns the role names available for assignment, in the order they should appear.</summary>
    IReadOnlyList<string> GetRoleNames();
}
