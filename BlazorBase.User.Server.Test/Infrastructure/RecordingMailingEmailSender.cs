using System.Collections.Concurrent;
using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>Stands in for the SMTP sender of <c>BlazorBase.Mailing</c> and keeps every message instead of sending it.</summary>
public sealed class RecordingMailingEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Messages { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
