using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// <see cref="IEmailDispatcher"/> that hands the message to <see cref="IBackgroundEmailQueue"/>
/// instead of sending it. <see cref="BackgroundEmailSenderService"/> performs the actual send
/// later, so a completed <see cref="DispatchAsync"/> here only means "accepted", per the contract
/// documented on <see cref="IEmailDispatcher"/>.
/// </summary>
public class BackgroundEmailDispatcher(IBackgroundEmailQueue queue) : IEmailDispatcher
{
    #region Injects
    private readonly IBackgroundEmailQueue Queue = queue;
    #endregion

    /// <summary>
    /// The enqueue itself always runs to completion, deliberately ignoring
    /// <paramref name="cancellationToken"/>: a cancelled request must not drop a message that has
    /// already been accepted for sending.
    /// </summary>
    public ValueTask DispatchAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        Queue.EnqueueAsync(message, CancellationToken.None);
}
