namespace BlazorBase.Notifications.Server.Configuration;

/// <summary>
/// The Web Push Urgency header value (RFC 8030), a hint the push service and operating system may
/// use to decide whether to wake the device immediately or defer delivery to save battery.
/// </summary>
public enum PushUrgency
{
    VeryLow,
    Low,
    Normal,
    High,
}
