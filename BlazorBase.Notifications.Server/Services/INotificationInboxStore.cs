using BlazorBase.Notifications.Models;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Host contract for the in-app notification inbox. The framework does not own the host's
/// notification entity, translations or user model, so the host implements this against its own
/// schema and registers it in DI; <c>MapBlazorBaseNotifications()</c> calls it to serve
/// <c>GET /api/notifications/unread</c> and friends.
/// </summary>
public interface INotificationInboxStore
{
    /// <summary>Every unread notification addressed to <paramref name="userId"/>, newest first.</summary>
    Task<IReadOnlyList<InboxNotificationDto>> GetUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The number of unread notifications addressed to <paramref name="userId"/>.</summary>
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Marks the notification <paramref name="id"/> as read for <paramref name="userId"/>.</summary>
    Task MarkReadAsync(Guid userId, long id, CancellationToken cancellationToken = default);

    /// <summary>Dismisses the notification <paramref name="id"/> for <paramref name="userId"/>.</summary>
    Task DismissAsync(Guid userId, long id, CancellationToken cancellationToken = default);
}
