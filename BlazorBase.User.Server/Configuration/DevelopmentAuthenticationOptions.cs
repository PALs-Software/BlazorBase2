namespace BlazorBase.User.Server.Configuration;

/// <summary>
/// Lets a developer run the app without signing in: the client asks the server for a session
/// and gets a normal access token for the account described here. Bound from the
/// <c>DevelopmentAuthentication</c> configuration section, which belongs in
/// <c>appsettings.Development.json</c> only.
/// </summary>
/// <remarks>
/// This is a real sign-in for a real account, not a bypass — the token, the claims and the
/// authorization pipeline are exactly the ones production uses, so what a developer tests is
/// what ships. Switching <see cref="Roles"/> and restarting is how you try a feature as a
/// different role.
/// </remarks>
public class DevelopmentAuthenticationOptions
{
    public const string SectionName = "DevelopmentAuthentication";

    /// <summary>
    /// Off unless a host opts in. Registration throws when this is true outside the
    /// Development environment, so it cannot be switched on by accident in production.
    /// </summary>
    public bool Enabled { get; set; }

    public string Email { get; set; } = "developer@localhost";

    public string DisplayName { get; set; } = "Development User";

    /// <summary>
    /// The roles the account should hold. Synchronized on every sign-in — roles missing from
    /// this list are removed — so narrowing the list actually narrows what you can reach.
    /// Leave it out to get <see cref="DefaultRoles"/>.
    /// </summary>
    /// <remarks>
    /// Deliberately empty rather than pre-filled with the default: the configuration binder
    /// <em>appends</em> configured entries to a collection that already has items, for arrays as
    /// much as for lists. A default of <c>["Admin"]</c> here would turn a configured
    /// <c>["User"]</c> into <c>["Admin", "User"]</c> and quietly hand out admin rights. Read the
    /// effective set through <see cref="EffectiveRoles"/>.
    /// </remarks>
    public IList<string> Roles { get; set; } = [];

    public static IReadOnlyList<string> DefaultRoles { get; } = ["Admin"];

    /// <summary>
    /// The configured roles, or <see cref="DefaultRoles"/> when none are configured.
    /// </summary>
    public IReadOnlyList<string> EffectiveRoles => Roles.Count > 0 ? [.. Roles] : DefaultRoles;
}
