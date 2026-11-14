using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using BlazorBase.User.Server.Entities;
using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Adapts Identity's <see cref="IEmailSender{TUser}"/> onto the framework's own
/// <see cref="IEmailDispatcher"/> rather than opening a second mail path. The emails are the standard
/// templates of <c>BlazorBase.Mailing</c> (<see cref="IAccountEmailBuilder"/>), rendered in the user's
/// <see cref="BaseUser.Language"/> with the configured <see cref="EmailBrandingOptions"/>; links are
/// HTML-encoded by the renderer. With the default <see cref="EmailDispatchMode.Background"/> dispatch
/// mode, the <see cref="Task"/> returned by every member below completes once the message has been
/// enqueued, not once it has been delivered. Register through <c>AddBlazorBaseMailingEmailSender</c>
/// once a host already has <c>BlazorBase.Mailing</c> configured.
/// </summary>
public class MailingEmailSender<TUser>(IEmailDispatcher emailDispatcher, IAccountEmailBuilder accountEmailBuilder) : IEmailSender<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly IEmailDispatcher EmailDispatcher = emailDispatcher;
    private readonly IAccountEmailBuilder AccountEmailBuilder = accountEmailBuilder;
    #endregion

    public async Task SendConfirmationLinkAsync(TUser user, string email, string confirmationLink)
    {
        var model = new EmailConfirmationEmailModel { RecipientName = user.DisplayName, ConfirmationLink = confirmationLink };
        var message = await AccountEmailBuilder.BuildEmailConfirmationAsync(email, model, EmailCultureResolver.Resolve(user.Language));
        await EmailDispatcher.DispatchAsync(message);
    }

    public async Task SendPasswordResetLinkAsync(TUser user, string email, string resetLink)
    {
        var model = new PasswordResetEmailModel { RecipientName = user.DisplayName, ResetLink = resetLink };
        var message = await AccountEmailBuilder.BuildPasswordResetAsync(email, model, EmailCultureResolver.Resolve(user.Language));
        await EmailDispatcher.DispatchAsync(message);
    }

    public async Task SendPasswordResetCodeAsync(TUser user, string email, string resetCode)
    {
        var model = new PasswordResetEmailModel { RecipientName = user.DisplayName, ResetCode = resetCode };
        var message = await AccountEmailBuilder.BuildPasswordResetAsync(email, model, EmailCultureResolver.Resolve(user.Language));
        await EmailDispatcher.DispatchAsync(message);
    }
}
