using System.Net.Http.Json;
using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Services;

/// <summary>
/// HTTP implementation of <see cref="IPushSubscriptionClient"/>. The injected <see cref="HttpClient"/>
/// must be pre-configured by the host with the correct base address (<c>…/api/notifications/</c>,
/// trailing slash included) and authentication handler; this class contains no auth logic.
/// </summary>
public class HttpPushSubscriptionClient(HttpClient httpClient) : IPushSubscriptionClient
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    /// <inheritdoc/>
    public async Task<string> GetPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.GetFromJsonAsync<PushPublicKeyResponse>("push/public-key", cancellationToken).ConfigureAwait(false);
        return response?.PublicKey ?? string.Empty;
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(PushSubscriptionDto subscription, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsJsonAsync("push/subscription", subscription, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task UnsubscribeAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var requestUri = $"push/subscription?endpoint={Uri.EscapeDataString(endpoint)}";
        var response = await HttpClient.DeleteAsync(requestUri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task SetEnabledAsync(string endpoint, bool enabled, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PutAsJsonAsync("push/subscription/enabled", new SetPushSubscriptionEnabledRequest(endpoint, enabled), cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
