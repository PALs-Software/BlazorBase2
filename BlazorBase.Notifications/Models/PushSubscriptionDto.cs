namespace BlazorBase.Notifications.Models;

/// <summary>
/// A browser Push API subscription as sent to <c>POST /api/notifications/push/subscription</c>.
/// The acting user is never part of this payload: it is resolved server-side from the caller's
/// claims, so a request can never register or move a subscription onto another user.
/// </summary>
/// <param name="Endpoint">The push service URL that uniquely identifies this subscription.</param>
/// <param name="P256dh">The subscription's P-256 Diffie-Hellman public key, base64url-encoded.</param>
/// <param name="Auth">The subscription's authentication secret, base64url-encoded.</param>
/// <param name="UserAgent">Optional free-text identifying the browser/device, for the user's own settings list.</param>
public sealed record PushSubscriptionDto(string Endpoint, string P256dh, string Auth, string? UserAgent);
