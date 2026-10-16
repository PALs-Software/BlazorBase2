using BlazorBase.Notifications.Server.Entities;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Abstraction over the host's <see cref="Microsoft.EntityFrameworkCore.DbContext"/> for
/// <see cref="PushSubscription"/> rows. Every method that targets a specific subscription by
/// endpoint is scoped to one <paramref name="userId" /> acting as the caller: a subscription never
/// becomes readable, modifiable or removable to a user other than the one it belongs to through
/// this interface.
/// </summary>
public interface IPushSubscriptionStore
{
    /// <summary>
    /// Creates a subscription for <paramref name="userId"/>, or updates it in place if a
    /// subscription with the same <paramref name="endpoint"/> already exists — re-subscribing from
    /// the same browser reuses the same endpoint, including after switching which user is signed in
    /// on that device, in which case ownership moves to the calling user.
    /// </summary>
    Task UpsertAsync(Guid userId, string endpoint, string p256dh, string auth, string? userAgent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the subscription at <paramref name="endpoint"/> if it belongs to <paramref name="userId"/>.
    /// </summary>
    /// <returns><see langword="true"/> if a row owned by <paramref name="userId"/> was removed.</returns>
    Task<bool> RemoveByEndpointAsync(Guid userId, string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Removes a subscription by its own id, regardless of owner. Used when a push service reports it gone.</summary>
    Task RemoveAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Enables or disables the subscription at <paramref name="endpoint"/> if it belongs to <paramref name="userId"/>.
    /// </summary>
    /// <returns><see langword="true"/> if a row owned by <paramref name="userId"/> was updated.</returns>
    Task<bool> SetEnabledAsync(Guid userId, string endpoint, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>All subscriptions belonging to <paramref name="userId"/>, enabled or not.</summary>
    Task<IReadOnlyList<PushSubscription>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Every subscription across every user, enabled or not.</summary>
    Task<IReadOnlyList<PushSubscription>> GetAllAsync(CancellationToken cancellationToken = default);
}
