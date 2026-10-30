using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Services;

/// <summary>
/// Client-side contract for Web Push subscription management, against the host's
/// <c>/api/notifications/push/*</c> endpoints mapped by <c>BlazorBase.Notifications.Server</c>'s
/// <c>MapBlazorBaseNotifications()</c>.
/// </summary>
public interface IPushSubscriptionClient
{
    /// <summary>The VAPID public key the browser needs to create a Push API subscription.</summary>
    Task<string> GetPublicKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>Registers or updates a Push API subscription for the signed-in user.</summary>
    Task SubscribeAsync(PushSubscriptionDto subscription, CancellationToken cancellationToken = default);

    /// <summary>Removes the subscription at <paramref name="endpoint"/>, if it belongs to the signed-in user.</summary>
    Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default);

    /// <summary>Enables or disables push delivery to the subscription at <paramref name="endpoint"/> without removing it.</summary>
    Task SetEnabledAsync(string endpoint, bool enabled, CancellationToken cancellationToken = default);
}
