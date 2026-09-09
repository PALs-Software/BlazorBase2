using BlazorBase.Mailing.Models;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Adapts Identity's <see cref="IEmailSender{TUser}"/> onto the framework's own
/// <see cref="BlazorBase.Mailing.Services.IEmailDispatcher"/> rather than opening a second mail path.
/// With the default <see cref="BlazorBase.Mailing.Models.EmailDispatchMode.Background"/> dispatch
/// mode, the <see cref="Task"/> returned by every member below completes once the message has been
/// enqueued, not once it has been delivered. Register through <c>AddBlazorBaseMailingEmailSender</c>
/// once a host already has <c>BlazorBase.Mailing</c> configured.
/// </summary>
public class MailingEmailSender<TUser>(BlazorBase.Mailing.Services.IEmailDispatcher emailDispatcher) : IEmailSender<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly BlazorBase.Mailing.Services.IEmailDispatcher EmailDispatcher = emailDispatcher;
    #endregion

    public Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink) =>
        DispatchAsync(email, "Confirm your email", $"Confirm your account by <a href=\"{confirmationLink}\">clicking here</a>.");

    public Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink) =>
        DispatchAsync(email, "Reset your password", $"Reset your password by <a href=\"{resetLink}\">clicking here</a>.");

    public Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode) =>
        DispatchAsync(email, "Your password reset code", $"Your password reset code is: {resetCode}");

    private Task DispatchAsync(string email, string subject, string htmlBody) =>
        EmailDispatcher.DispatchAsync(new EmailMessage
        {
            To = [email],
            Subject = subject,
            HtmlBody = htmlBody,
        }).AsTask();
}
