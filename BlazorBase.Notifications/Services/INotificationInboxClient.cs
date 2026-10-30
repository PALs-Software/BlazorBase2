using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Services;

/// <summary>
/// Client-side contract for the in-app notification inbox, against the host's
/// <c>GET /api/notifications/unread</c> endpoints mapped by
/// <c>BlazorBase.Notifications.Server</c>'s <c>MapBlazorBaseNotifications()</c>.
/// </summary>
public interface INotificationInboxClient
{
    /// <summary>Every unread notification addressed to the signed-in user, newest first.</summary>
    Task<IReadOnlyList<InboxNotificationDto>> GetUnreadAsync(CancellationToken cancellationToken = default);

    /// <summary>The number of unread notifications addressed to the signed-in user.</summary>
    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    /// <summary>Marks the notification <paramref name="id"/> as read.</summary>
    Task MarkReadAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Dismisses the notification <paramref name="id"/>.</summary>
    Task DismissAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Dismisses every notification that is currently unread for the signed-in user.</summary>
    Task DismissAllAsync(CancellationToken cancellationToken = default);
}
