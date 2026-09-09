using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// <see cref="IEmailSender"/> test double intended to be registered scoped. Delegates recording
/// to the injected singleton <see cref="EmailSendRecorder"/> so tests can observe sends across
/// every scope the container creates, while each resolved <see cref="RecordingEmailSender"/>
/// instance is itself distinct per scope.
/// </summary>
public sealed class RecordingEmailSender(EmailSendRecorder recorder) : IEmailSender
{
    private readonly EmailSendRecorder Recorder = recorder;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var exception = Recorder.RecordSend(this, message);

        if (exception is not null)
            throw exception;

        return Task.CompletedTask;
    }
}
