using System.Globalization;
using BlazorBase.Mailing.Models;

namespace BlazorBase.Mailing.Services;

/// <summary>
/// Builds the standard account emails - invitation, password reset, email confirmation - as ready-to-send
/// <see cref="EmailMessage"/>s: the subject and body are rendered from the localized templates in
/// <c>BlazorBase.Mailing.Templates.Account</c> with the configured <see cref="EmailBrandingOptions"/>.
/// </summary>
/// <remarks>
/// <paramref name="culture"/> on every member selects the language of subject and body; <see langword="null"/>
/// keeps the ambient UI culture. Links are HTML-encoded by the renderer, never concatenated into markup.
/// </remarks>
public interface IAccountEmailBuilder
{
    Task<EmailMessage> BuildInvitationAsync(string recipientEmail, InvitationEmailModel model, CultureInfo? culture = null);

    Task<EmailMessage> BuildPasswordResetAsync(string recipientEmail, PasswordResetEmailModel model, CultureInfo? culture = null);

    Task<EmailMessage> BuildEmailConfirmationAsync(string recipientEmail, EmailConfirmationEmailModel model, CultureInfo? culture = null);
}
