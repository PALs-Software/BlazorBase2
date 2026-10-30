using BlazorBase.Notifications.Models;
using Microsoft.JSInterop;

namespace BlazorBase.Notifications.Interop;

/// <summary>
/// JS-module wrapper for <c>blazorbase-notifications.js</c>, backing <see cref="IWebPushClient"/>.
/// </summary>
public sealed class WebPushClient(IJSRuntime jsRuntime) : IWebPushClient, IAsyncDisposable
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string ModulePath = "./_content/BlazorBase.Notifications/blazorbase-notifications.js";

    private Task<IJSObjectReference>? ModuleTask;

    /// <inheritdoc/>
    public async ValueTask<bool> IsSupportedAsync()
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        return await module.InvokeAsync<bool>("isSupported").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<WebPushPermission> GetPermissionAsync()
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        var permission = await module.InvokeAsync<string>("getPermission").ConfigureAwait(false);
        return permission switch
        {
            "granted" => WebPushPermission.Granted,
            "denied" => WebPushPermission.Denied,
            _ => WebPushPermission.Default,
        };
    }

    /// <inheritdoc/>
    public async ValueTask<PushSubscriptionDto> SubscribeAsync(string vapidPublicKey)
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        return await module.InvokeAsync<PushSubscriptionDto>("subscribe", vapidPublicKey).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> UnsubscribeAsync()
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        return await module.InvokeAsync<bool>("unsubscribe").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<string?> GetCurrentEndpointAsync()
    {
        var module = await GetModuleAsync().ConfigureAwait(false);
        return await module.InvokeAsync<string?>("getCurrentEndpoint").ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (ModuleTask is null)
            return;

        try
        {
            var module = await ModuleTask.ConfigureAwait(false);
            await module.DisposeAsync().ConfigureAwait(false);
        }
        catch (JSDisconnectedException)
        {
        }
    }

    private Task<IJSObjectReference> GetModuleAsync()
        => ModuleTask ??= JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
}
