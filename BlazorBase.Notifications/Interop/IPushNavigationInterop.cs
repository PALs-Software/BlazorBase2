using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// Lets <see cref="Components.PushNavigationListener.PushNavigationListener"/> receive the
/// <c>blazorbase-push-navigate</c> message the push service-worker handler posts to an open window
/// when the user taps a native notification, so the app can navigate internally.
/// </summary>
public interface IPushNavigationInterop : IAsyncDisposable
{
    /// <summary>
    /// Starts listening for service-worker navigate messages, acknowledging each one to the service
    /// worker and invoking <c>OnNavigateRequested</c> on <paramref name="callbackTarget"/> with the
    /// absolute same-origin URL. Messages that point to another origin are ignored and left
    /// unacknowledged.
    /// </summary>
    ValueTask RegisterAsync(DotNetObjectReference<Components.PushNavigationListener.PushNavigationListener> callbackTarget);
}
