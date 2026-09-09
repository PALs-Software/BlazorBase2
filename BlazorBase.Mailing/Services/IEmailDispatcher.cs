using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Accepts an <see cref="EmailMessage"/> for sending. A completed <see cref="DispatchAsync"/>
/// means the message has been handed off, never that it has arrived: with
/// <see cref="EmailDispatchMode.Background"/> the actual <see cref="IEmailSender.SendAsync"/>
/// call happens later, on a different scope, after this method has already returned. Callers
/// that need delivery confirmed synchronously must not use this abstraction; that is what
/// <see cref="EmailDispatchMode.Inline"/> and <see cref="InlineEmailDispatcher"/> are for.
/// </summary>
public interface IEmailDispatcher
{
    ValueTask DispatchAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
