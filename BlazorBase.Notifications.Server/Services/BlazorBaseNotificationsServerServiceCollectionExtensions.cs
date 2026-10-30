using BlazorBase.Notifications.Server.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using WebPush;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Extension methods to register the <c>BlazorBase.Notifications.Server</c> module with the
/// dependency injection container.
/// </summary>
public static class BlazorBaseNotificationsServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers all BlazorBase.Notifications server-side services: <see cref="WebPushOptions"/>
    /// (bound from <paramref name="configuration"/>'s <paramref name="sectionName"/> section, then
    /// optionally overridden by <paramref name="configure"/>), the Web Push client and sender, the
    /// push notifier, and the EF-backed subscription store.
    /// </summary>
    /// <remarks>
    /// The host must call <c>AddDbContext&lt;TContext&gt;()</c> for its concrete context (which must
    /// expose a <c>DbSet&lt;PushSubscription&gt;</c> and call
    /// <c>ApplyPushSubscriptionConfiguration(modelBuilder)</c> in its <c>OnModelCreating</c>
    /// override) before calling <see cref="AddBlazorBaseNotificationsServer{TContext}"/>. This
    /// method bridges the base <see cref="DbContext"/> to the host's scoped
    /// <typeparamref name="TContext"/> registration, so <see cref="EfPushSubscriptionStore"/> can
    /// resolve a scoped <see cref="DbContext"/> without any separate registration by the host. The
    /// host must additionally register its own <see cref="INotificationInboxStore"/> implementation
    /// and call <c>MapBlazorBaseNotifications()</c> to expose the REST surface.
    /// <para>
    /// A default <see cref="INotificationUserIdResolver"/> (<see cref="ClaimsNotificationUserIdResolver"/>)
    /// is registered with <c>TryAdd</c>, so a host whose identity user id and domain user id differ
    /// can register its own implementation before calling this method to have it used instead.
    /// </para>
    /// <para>
    /// VAPID configuration: add a <c>BlazorBaseNotifications:WebPush:Subject/PublicKey/PrivateKey</c>
    /// section (generate a key pair once with <see cref="VapidKeyGenerator.Generate"/> and keep it
    /// stable) to application secrets or environment variables, or use the
    /// <paramref name="configure"/> delegate. Startup fails fast with a descriptive error if any of
    /// the three is missing.
    /// </para>
    /// </remarks>
    /// <typeparam name="TContext">The host's concrete <see cref="DbContext"/> type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root to bind <see cref="WebPushOptions"/> from.</param>
    /// <param name="sectionName">Configuration section name for <see cref="WebPushOptions"/>. Defaults to <see cref="WebPushOptions.SectionName"/>.</param>
    /// <param name="configure">Optional delegate to override bound <see cref="WebPushOptions"/> values.</param>
    public static IServiceCollection AddBlazorBaseNotificationsServer<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = WebPushOptions.SectionName,
        Action<WebPushOptions>? configure = null)
        where TContext : DbContext
    {
        services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TContext>());
        services.AddScoped<IPushSubscriptionStore, EfPushSubscriptionStore>();
        services.TryAddScoped<INotificationUserIdResolver, ClaimsNotificationUserIdResolver>();

        services.AddSingleton<IWebPushClient, WebPushClient>();
        services.AddSingleton<IWebPushSender, WebPushSender>();
        services.AddScoped<IPushNotifier, PushNotifier>();

        var optionsBuilder = services.AddOptions<WebPushOptions>().Bind(configuration.GetSection(sectionName));
        if (configure is not null)
            optionsBuilder.Configure(configure);
        optionsBuilder.ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<WebPushOptions>, WebPushOptionsValidator>());

        return services;
    }
}
