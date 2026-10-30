namespace BlazorBase.Notifications.Server.Configuration;

/// <summary>
/// VAPID identity and default delivery tuning for outgoing Web Push messages. Bound from
/// configuration by <see cref="Services.BlazorBaseNotificationsServerServiceCollectionExtensions.AddBlazorBaseNotificationsServer{TContext}"/>.
/// </summary>
public class WebPushOptions
{
    /// <summary>Default configuration section.</summary>
    public const string SectionName = "BlazorBaseNotifications:WebPush";

    /// <summary>
    /// The VAPID subject identifying the sender to a push service, for example
    /// <c>mailto:support@example.com</c> or an <c>https://</c> URL.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>URL-safe base64-encoded VAPID public key, handed to the browser to create a subscription.</summary>
    public string? PublicKey { get; set; }

    /// <summary>URL-safe base64-encoded VAPID private key. Never exposed to a client.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>
    /// How long a push service may retain an undelivered message, in seconds. Defaults to 4 weeks,
    /// the same default the underlying Web Push protocol uses when no TTL is given.
    /// </summary>
    public int TimeToLiveSeconds { get; set; } = 2419200;

    /// <summary>Delivery urgency hint sent with every push message. Defaults to <see cref="PushUrgency.Normal"/>.</summary>
    public PushUrgency Urgency { get; set; } = PushUrgency.Normal;
}
