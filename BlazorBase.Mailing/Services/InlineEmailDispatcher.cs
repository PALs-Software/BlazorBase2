using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// <see cref="IEmailDispatcher"/> that awaits <see cref="IEmailSender.SendAsync"/> directly on the
/// caller's scope. This is the escape hatch for a host that needs the pre-dispatcher behaviour:
/// send latency counted as part of the caller's response, and send failures propagated as
/// exceptions instead of only being logged by the background worker.
/// </summary>
public class InlineEmailDispatcher(IEmailSender emailSender) : IEmailDispatcher
{
    #region Injects
    private readonly IEmailSender EmailSender = emailSender;
    #endregion

    public async ValueTask DispatchAsync(EmailMessage message, CancellationToken cancellationToken = default) =>
        await EmailSender.SendAsync(message, cancellationToken);
}
