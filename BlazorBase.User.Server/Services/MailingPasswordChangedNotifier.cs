using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.Services;

/// <summary>
/// Renders the standard "your password was changed" email through <see cref="IAccountEmailBuilder"/> in the
/// user's <see cref="BaseUser.Language"/> and hands it to <see cref="IEmailDispatcher"/>. With the default
/// background dispatch the <see cref="Task"/> completes once the message is enqueued, not delivered.
/// Registered by <c>AddBlazorBaseMailingEmailSender</c>.
/// </summary>
public class MailingPasswordChangedNotifier<TUser>(IEmailDispatcher emailDispatcher, IAccountEmailBuilder accountEmailBuilder) : IPasswordChangedNotifier<TUser>
    where TUser : BaseUser
{
    #region Injects
    private readonly IEmailDispatcher EmailDispatcher = emailDispatcher;
    private readonly IAccountEmailBuilder AccountEmailBuilder = accountEmailBuilder;
    #endregion

    public async Task NotifyPasswordChangedAsync(TUser user, string email, CancellationToken cancellationToken = default)
    {
        var model = new PasswordChangedEmailModel { RecipientName = user.DisplayName };
        var message = await AccountEmailBuilder.BuildPasswordChangedAsync(email, model, EmailCultureResolver.Resolve(user.Language));
        await EmailDispatcher.DispatchAsync(message, cancellationToken);
    }
}
