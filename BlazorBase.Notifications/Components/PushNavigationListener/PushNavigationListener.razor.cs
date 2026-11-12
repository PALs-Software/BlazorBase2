using BlazorBase.Notifications.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Components.PushNavigationListener;

/// <summary>
/// Renders nothing. While it is part of the page, tapping a native push notification navigates the
/// already open app through <see cref="NavigationManager"/> instead of reloading it, so the app's
/// <c>NavigationLock</c> and client-side routing apply. Place it once, in a layout that is always
/// rendered interactively (for example next to <c>NotificationBell</c>). A page without the listener
/// still works: the service worker falls back to a full-page navigation when the message is not
/// acknowledged within one second.
/// </summary>
public partial class PushNavigationListener(
    IPushNavigationInterop navigationInterop,
    NavigationManager navigationManager) : ComponentBase, IAsyncDisposable
{
    #region Injects
    private readonly IPushNavigationInterop NavigationInterop = navigationInterop;
    private readonly NavigationManager NavigationManager = navigationManager;
    #endregion

    private DotNetObjectReference<PushNavigationListener>? CallbackReference;

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        CallbackReference = DotNetObjectReference.Create(this);
        await NavigationInterop.RegisterAsync(CallbackReference);
    }

    /// <summary>
    /// Called by the navigation interop when the push service worker asks the app to open
    /// <paramref name="url"/>. Absolute URLs inside the application base and base-relative or
    /// root-relative URLs navigate internally; URLs on another origin are ignored.
    /// </summary>
    /// <param name="url">The target URL, absolute or relative.</param>
    [JSInvokable]
    public Task OnNavigateRequested(string url) => InvokeAsync(() => NavigateTo(url));

    private void NavigateTo(string url)
    {
        var baseUri = new Uri(NavigationManager.BaseUri);
        var targetUri = NavigationManager.ToAbsoluteUri(url);

        if (!string.Equals(baseUri.GetLeftPart(UriPartial.Authority), targetUri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
            return;

        if (!baseUri.IsBaseOf(targetUri))
        {
            NavigationManager.NavigateTo(targetUri.ToString());
            return;
        }

        NavigationManager.NavigateTo(NavigationManager.ToBaseRelativePath(targetUri.ToString()));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await NavigationInterop.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }

        CallbackReference?.Dispose();
        GC.SuppressFinalize(this);
    }
}
