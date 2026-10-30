namespace BlazorBase.Notifications.Models;

/// <summary>
/// Response of <c>GET /api/notifications/push/public-key</c>: the VAPID public key a browser needs
/// to create a Push API subscription. Never includes the private key.
/// </summary>
/// <param name="PublicKey">URL-safe base64-encoded VAPID public key.</param>
public sealed record PushPublicKeyResponse(string PublicKey);
