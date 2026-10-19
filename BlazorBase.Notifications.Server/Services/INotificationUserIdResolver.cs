using System.Security.Claims;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Resolves the acting user id for a notification or push-subscription request.
/// <c>MapBlazorBaseNotifications</c> calls this for every endpoint instead of reading claims
/// itself, so a host whose identity user id and domain user id differ can register its own
/// implementation (translating through its own user store) instead of the default claims-only
/// behavior.
/// </summary>
public interface INotificationUserIdResolver
{
    /// <summary>
    /// Resolves the acting user id for <paramref name="user"/>.
    /// </summary>
    /// <returns>
    /// The resolved user id, or <see langword="null"/> if it cannot be resolved, in which case the
    /// endpoint treats the request as unauthenticated (401).
    /// </returns>
    Task<Guid?> ResolveUserIdAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}
