using System.Net.Http.Json;
using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Services;

/// <summary>
/// HTTP implementation of <see cref="INotificationInboxClient"/>. The injected <see cref="HttpClient"/>
/// must be pre-configured by the host with the correct base address (<c>…/api/notifications/</c>,
/// trailing slash included) and authentication handler; this class contains no auth logic.
/// </summary>
public class HttpNotificationInboxClient(HttpClient httpClient) : INotificationInboxClient
{
    #region Injects
    private readonly HttpClient HttpClient = httpClient;
    #endregion

    /// <inheritdoc/>
    public async Task<IReadOnlyList<InboxNotificationDto>> GetUnreadAsync(CancellationToken cancellationToken = default)
    {
        var notifications = await HttpClient.GetFromJsonAsync<IReadOnlyList<InboxNotificationDto>>("unread", cancellationToken).ConfigureAwait(false);
        return notifications ?? [];
    }

    /// <inheritdoc/>
    public async Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default)
        => await HttpClient.GetFromJsonAsync<int>("unread/count", cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task MarkReadAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"{id}/read", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async Task DismissAsync(long id, CancellationToken cancellationToken = default)
    {
        var response = await HttpClient.PostAsync($"{id}/dismiss", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }
}
