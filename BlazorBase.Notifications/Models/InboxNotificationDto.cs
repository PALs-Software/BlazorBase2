namespace BlazorBase.Notifications.Models;

/// <summary>
/// One in-app notification as shown in the inbox. The host owns the underlying entity and
/// translation model; this is the wire shape returned by <c>GET /api/notifications/unread</c>.
/// </summary>
/// <param name="Id">Stable identifier assigned by the host.</param>
/// <param name="Title">Short heading.</param>
/// <param name="Message">Body, rendered as HTML.</param>
/// <param name="Type">Host-defined category, for example <c>Info</c> or <c>Warning</c>.</param>
/// <param name="CreatedAt">When the notification was created, in UTC.</param>
/// <param name="IsRead">Whether the acting user has already read this notification.</param>
public sealed record InboxNotificationDto(long Id, string Title, string Message, string Type, DateTime CreatedAt, bool IsRead);
