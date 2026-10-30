namespace BlazorBase.Notifications.Models;

/// <summary>
/// Client-side tuning for <see cref="Components.NotificationBell.NotificationBell"/>, registered by
/// <see cref="Services.BlazorBaseNotificationsServiceCollectionExtensions.AddBlazorBaseNotifications"/>.
/// </summary>
public class NotificationClientOptions
{
    /// <summary>
    /// How often the inbox is polled for unread notifications, in addition to the refresh that
    /// happens on tab focus and on a service-worker push message. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
}
