using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// Lets <see cref="Components.NotificationBell.NotificationBell"/> refresh immediately instead of
/// waiting for the next poll: once when the tab becomes visible again, and once per message the
/// framework's push service-worker handler posts to open clients after showing a notification.
/// </summary>
public interface INotificationRefreshInterop : IAsyncDisposable
{
    /// <summary>
    /// Starts listening for <c>visibilitychange</c>-to-visible and service-worker messages, invoking
    /// <c>OnRefreshRequested</c> on <paramref name="callbackTarget"/> for each.
    /// </summary>
    ValueTask RegisterAsync(DotNetObjectReference<Components.NotificationBell.NotificationBell> callbackTarget);
}
