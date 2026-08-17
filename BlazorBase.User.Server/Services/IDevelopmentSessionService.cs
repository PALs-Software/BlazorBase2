using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Provides the account a developer is signed in as when
/// <see cref="Configuration.DevelopmentAuthenticationOptions.Enabled"/> is set.
/// </summary>
public interface IDevelopmentSessionService<TUser> where TUser : BaseUser
{
    /// <summary>
    /// True only when the option is set <em>and</em> the app runs in the Development
    /// environment. Everything else on this interface is gated on it.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Returns the configured development account, creating it if it does not exist yet and
    /// synchronizing its roles with the configuration. Returns null when
    /// <see cref="IsEnabled"/> is false, or when the account could not be created.
    /// </summary>
    Task<TUser?> ResolveUserAsync(CancellationToken cancellationToken = default);
}
