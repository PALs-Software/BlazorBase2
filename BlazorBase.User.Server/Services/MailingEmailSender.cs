using BlazorBase.Mailing.Models;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Adapts Identity's <see cref="IEmailSender{TUser}"/> onto the framework's own
/// <see cref="BlazorBase.Mailing.Services.IEmailSender"/> rather than opening a second mail path.
/// Register through <c>AddBlazorBaseMailingEmailSender</c> once a host already has
/// <c>BlazorBase.Mailing</c> configured.
/// </summary>
public class MailingEmailSender<TUser>(BlazorBase.Mailing.Services.IEmailSender emailSender) : IEmailSender<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly BlazorBase.Mailing.Services.IEmailSender EmailSender = emailSender;
    #endregion

    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email", $"Confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.");

    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password", $"Reset your password by <a href=\"{resetLink}\">clicking here</a>.");

    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
        SendAsync(email, "Your password reset code", $"Your password reset code is: {resetCode}");

    private Task SendAsync(string email, string subject, string htmlBody) =>
        EmailSender.SendAsync(new EmailMessage
        {
            To = [email],
            Subject = subject,
            HtmlBody = htmlBody,
        });
}
