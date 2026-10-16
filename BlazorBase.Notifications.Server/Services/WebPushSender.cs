using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using BlazorBase.Notifications.Server.Configuration;
using Microsoft.Extensions.Options;
using WebPush;
using PushSubscription = BlazorBase.Notifications.Server.Entities.PushSubscription;
using WebPushSubscription = WebPush.PushSubscription;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// <see cref="IWebPushSender"/> implementation on top of the <c>WebPush</c> NuGet package
/// (web-push-libs/web-push-csharp), the .NET port of the reference Node.js <c>web-push</c> library
/// used by the official VAPID/Web Push samples.
/// </summary>
public sealed class WebPushSender(IWebPushClient webPushClient, IOptions<WebPushOptions> options) : IWebPushSender
{
    #region Injects
    private readonly IWebPushClient WebPushClient = webPushClient;
    private readonly IOptions<WebPushOptions> Options = options;
    #endregion

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <inheritdoc/>
    public async Task<PushSendResult> SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken)
    {
        var settings = Options.Value;
        var vapidDetails = new VapidDetails(settings.Subject, settings.PublicKey, settings.PrivateKey);
        var webPushSubscription = new WebPushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth);
        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        var sendOptions = new Dictionary<string, object>
        {
            ["vapidDetails"] = vapidDetails,
            ["TTL"] = settings.TimeToLiveSeconds,
            ["headers"] = new Dictionary<string, object> { ["Urgency"] = UrgencyHeaderValue(settings.Urgency) },
        };

        try
        {
            await WebPushClient.SendNotificationAsync(webPushSubscription, json, sendOptions, cancellationToken).ConfigureAwait(false);
            return PushSendResult.Sent;
        }
        catch (WebPushException exception) when (exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            return PushSendResult.SubscriptionGone;
        }
        catch (WebPushException)
        {
            return PushSendResult.Failed;
        }
        catch (HttpRequestException)
        {
            return PushSendResult.Failed;
        }
    }

    private static string UrgencyHeaderValue(PushUrgency urgency) => urgency switch
    {
        PushUrgency.VeryLow => "very-low",
        PushUrgency.Low => "low",
        PushUrgency.High => "high",
        _ => "normal",
    };
}
