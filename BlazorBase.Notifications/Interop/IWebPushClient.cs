using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// Browser-side seam for Web Push: support detection, permission state, and the subscribe /
/// unsubscribe round trip against the browser's <c>PushManager</c>. Implementations never request
/// notification permission except from inside <see cref="SubscribeAsync"/>, so permission is only
/// ever asked while the call stack still carries the user gesture that triggered it (a toggle click),
/// never on page load.
/// </summary>
public interface IWebPushClient
{
    /// <summary>
    /// Whether this browser supports the Push API, service workers and the Notifications API at
    /// all. A host should hide push-related UI entirely when this is <see langword="false"/>.
    /// </summary>
    ValueTask<bool> IsSupportedAsync();

    /// <summary>The current notification permission for this origin.</summary>
    ValueTask<WebPushPermission> GetPermissionAsync();

    /// <summary>
    /// Requests notification permission if it has not been decided yet, then creates a Push API
    /// subscription against the ready service worker registration using
    /// <paramref name="vapidPublicKey"/> as the application server key.
    /// </summary>
    /// <param name="vapidPublicKey">URL-safe base64-encoded VAPID public key.</param>
    /// <exception cref="Microsoft.JSInterop.JSException">
    /// The user denied permission, or subscribing failed for any other reason.
    /// </exception>
    ValueTask<PushSubscriptionDto> SubscribeAsync(string vapidPublicKey);

    /// <summary>
    /// Removes the current Push API subscription from this browser, if any.
    /// </summary>
    /// <returns><see langword="true"/> if a subscription existed and was removed.</returns>
    ValueTask<bool> UnsubscribeAsync();

    /// <summary>
    /// The endpoint of the current Push API subscription in this browser, or
    /// <see langword="null"/> when there is none.
    /// </summary>
    ValueTask<string?> GetCurrentEndpointAsync();
}
