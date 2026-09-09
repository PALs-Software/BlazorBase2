using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Test.Infrastructure;

/// <summary>
/// Singleton collaborator shared by every scoped <see cref="RecordingEmailSender"/> instance the
/// container creates. Recording lives here rather than on the sender itself so a test can prove
/// that <see cref="BlazorBase.Mailing.Services.BackgroundEmailSenderService"/> resolves a fresh
/// scope, and therefore a fresh sender instance, per message.
/// </summary>
public sealed class EmailSendRecorder
{
    private readonly Dictionary<EmailMessage, TaskCompletionSource<EmailMessage>> Signals =
        new(ReferenceEqualityComparer.Instance);

    private int CallCount;

    public List<EmailMessage> SentMessages { get; } = [];

    public List<RecordingEmailSender> ResolvedSenders { get; } = [];

    public Func<int, Exception?> ExceptionForCall { get; set; } = _ => null;

    /// <summary>
    /// Returns a task that completes once <paramref name="message"/> has actually reached
    /// <see cref="RecordingEmailSender.SendAsync"/>, keyed by reference identity so multiple
    /// in-flight messages cannot be confused with one another. Callers await this instead of
    /// sleeping for a fixed duration.
    /// </summary>
    public Task<EmailMessage> WaitForSendAsync(EmailMessage message)
    {
        var signal = new TaskCompletionSource<EmailMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (Signals)
        {
            Signals[message] = signal;
        }

        return signal.Task;
    }

    public Exception? RecordSend(RecordingEmailSender sender, EmailMessage message)
    {
        var callIndex = Interlocked.Increment(ref CallCount) - 1;

        lock (SentMessages)
        {
            SentMessages.Add(message);
            ResolvedSenders.Add(sender);
        }

        lock (Signals)
        {
            if (Signals.TryGetValue(message, out var signal))
                signal.TrySetResult(message);
        }

        return ExceptionForCall(callIndex);
    }
}
