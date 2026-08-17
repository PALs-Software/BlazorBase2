using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Sends transport-agnostic <see cref="EmailMessage"/> instances. The concrete implementation
/// (SMTP, SendGrid, log-only, etc.) is chosen via dependency injection.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
