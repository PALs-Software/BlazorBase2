using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// JS-module wrapper for <c>blazorbase-notifications.js</c>, backing <see cref="INotificationRefreshInterop"/>.
/// One instance registers at most one listener; disposing it removes that listener's DOM/service-worker
/// event handlers.
/// </summary>
public sealed class NotificationRefreshInterop(IJSRuntime jsRuntime) : INotificationRefreshInterop
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string ModulePath = "./_content/BlazorBase.Notifications/blazorbase-notifications.js";

    private readonly string ListenerId = Guid.NewGuid().ToString("N");

    private Task<IJSObjectReference>? ModuleTask;

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(DotNetObjectReference<Components.NotificationBell.NotificationBell> callbackTarget)
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        await module.InvokeVoidAsync("registerRefreshListener", ListenerId, callbackTarget).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ModuleTask is null)
            return;

        try
        {
            var module = await ModuleTask.ConfigureAwait(false);
            await module.InvokeVoidAsync("unregisterRefreshListener", ListenerId).ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
        => ModuleTask ??= JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
}
