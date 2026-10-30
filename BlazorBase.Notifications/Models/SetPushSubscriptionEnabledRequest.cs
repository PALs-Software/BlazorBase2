namespace BlazorBase.Notifications.Models;

/// <summary>
/// Request body for <c>PUT /api/notifications/push/subscription/enabled</c>: enables or disables
/// native push delivery to one of the caller's own subscriptions without removing it.
/// </summary>
/// <param name="Endpoint">The subscription's push service URL, identifying which one to toggle.</param>
/// <param name="Enabled">The new enabled state.</param>
public sealed record SetPushSubscriptionEnabledRequest(string Endpoint, bool Enabled);
