using BlazorBase.Notifications.Server.Entities;

namespace BlazorBase.Notifications.Server.Services;

/// <summary>
/// Sends one Web Push message to one subscription. Implementations never throw for a delivery
/// failure — the <see cref="PushSendResult"/> tells the caller what happened so one failing
/// subscription never stops the rest of a batch.
/// </summary>
public interface IWebPushSender
{
    /// <summary>Encrypts and sends <paramref name="payload"/> to <paramref name="subscription"/>.</summary>
    /// <param name="subscription">The target subscription.</param>
    /// <param name="payload">The message content.</param>
    /// <param name="cancellationToken">Cancels the send.</param>
    Task<PushSendResult> SendAsync(PushSubscription subscription, PushPayload payload, CancellationToken cancellationToken);
}
