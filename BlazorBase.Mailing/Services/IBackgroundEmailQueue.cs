using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Singleton hand-off point between <see cref="BackgroundEmailDispatcher"/> (producer) and
/// <see cref="BackgroundEmailSenderService"/> (consumer). Kept as its own abstraction, separate
/// from <see cref="IEmailDispatcher"/>, so the queuing mechanism can be swapped or unit-tested
/// without involving the dispatcher's public contract.
/// </summary>
public interface IBackgroundEmailQueue
{
    /// <summary>
    /// Enqueues a message. Must not be tied to a cancellation token scoped to the originating
    /// HTTP request: a client disconnecting or a request being cancelled after the message was
    /// already accepted for background sending must not drop it.
    /// </summary>
    ValueTask EnqueueAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams queued messages until <paramref name="cancellationToken"/> is cancelled, typically
    /// the host's shutdown token.
    /// </summary>
    IAsyncEnumerable<EmailMessage> DequeueAllAsync(CancellationToken cancellationToken);
}
