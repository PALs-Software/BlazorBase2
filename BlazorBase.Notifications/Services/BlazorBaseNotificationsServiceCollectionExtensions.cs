using BlazorBase.Notifications.Interop;
using BlazorBase.Notifications.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BlazorBase.Notifications.Services;

/// <summary>Registers the BlazorBase.Notifications client module.</summary>
public static class BlazorBaseNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Web Push JS interop seams, the notification refresh seam, and localized UI
    /// text for <see cref="Components.NotificationBell.NotificationBell"/> and
    /// <see cref="Components.PushSettingsToggle.PushSettingsToggle"/>.
    /// </summary>
    /// <remarks>
    /// The host must additionally register the <see cref="HttpClient"/>s behind
    /// <see cref="INotificationInboxClient"/> and <see cref="IPushSubscriptionClient"/>, because only
    /// the host knows the API's address and its authentication handler:
    /// <code>
    /// services.AddHttpClient&lt;INotificationInboxClient, HttpNotificationInboxClient&gt;(client =&gt;
    ///         client.BaseAddress = new Uri(new Uri(hostBaseAddress), "api/notifications/"))
    ///     .AddHttpMessageHandler&lt;AuthTokenHandler&gt;();
    /// services.AddHttpClient&lt;IPushSubscriptionClient, HttpPushSubscriptionClient&gt;(client =&gt;
    ///         client.BaseAddress = new Uri(new Uri(hostBaseAddress), "api/notifications/"))
    ///     .AddHttpMessageHandler&lt;AuthTokenHandler&gt;();
    /// </code>
    /// The server side is <c>MapBlazorBaseNotifications()</c> in <c>BlazorBase.Notifications.Server</c>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional delegate to override default <see cref="NotificationClientOptions"/> values.</param>
    public static IServiceCollection AddBlazorBaseNotifications(this IServiceCollection services, Action<NotificationClientOptions>? configure = null)
    {
        var options = new NotificationClientOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddLocalization();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<IWebPushClient, WebPushClient>();
        services.TryAddTransient<INotificationRefreshInterop, NotificationRefreshInterop>();

        return services;
    }
}
