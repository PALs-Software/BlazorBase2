namespace BlazorBase.Notifications.Server.Services;

/// <summary>Outcome of one <see cref="IWebPushSender"/> send attempt.</summary>
public enum PushSendResult
{
    /// <summary>The push service accepted the message.</summary>
    Sent,

    /// <summary>
    /// The push service answered 404 Not Found or 410 Gone: the subscription is no longer valid and
    /// must be removed.
    /// </summary>
    SubscriptionGone,

    /// <summary>The send failed for any other reason. The subscription is left untouched.</summary>
    Failed,
}
