using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// JS-module wrapper for <c>blazorbase-notifications.js</c>, backing <see cref="IPushNavigationInterop"/>.
/// One instance registers at most one listener; disposing it removes that listener's service-worker
/// message handler.
/// </summary>
public sealed class PushNavigationInterop(IJSRuntime jsRuntime) : IPushNavigationInterop
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string ModulePath = "./_content/BlazorBase.Notifications/blazorbase-notifications.js";

    private readonly string ListenerId = Guid.NewGuid().ToString("N");

    private Task<IJSObjectReference>? ModuleTask;

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(DotNetObjectReference<Components.PushNavigationListener.PushNavigationListener> callbackTarget)
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        await module.InvokeVoidAsync("registerNavigationListener", ListenerId, callbackTarget).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ModuleTask is null)
            return;

        try
        {
            var module = await ModuleTask.ConfigureAwait(false);
            await module.InvokeVoidAsync("unregisterNavigationListener", ListenerId).ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
        => ModuleTask ??= JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
}
