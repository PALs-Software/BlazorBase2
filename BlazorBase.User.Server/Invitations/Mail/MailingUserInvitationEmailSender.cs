using BlazorBase.Mailing.Models;
using BlazorBase.Mailing.Services;
using BlazorBase.User.Server.Services;

namespace BlazorBase.User.Server.Invitations.Mail;

/// <summary>
/// Renders the standard invitation email through <see cref="IAccountEmailBuilder"/> in the invitation's
/// language and hands it to <see cref="IEmailDispatcher"/>. With the default background dispatch,
/// <see langword="true"/> means the message was queued, not delivered.
/// </summary>
public sealed class MailingUserInvitationEmailSender(IAccountEmailBuilder accountEmailBuilder, IEmailDispatcher emailDispatcher) : IUserInvitationEmailSender
{
    #region Injects
    private readonly IAccountEmailBuilder AccountEmailBuilder = accountEmailBuilder;
    private readonly IEmailDispatcher EmailDispatcher = emailDispatcher;
    #endregion

    public async Task<bool> SendAsync(UserInvitationEmail email, CancellationToken cancellationToken = default)
    {
        var model = new InvitationEmailModel
        {
            RecipientName = email.RecipientName,
            InvitedByName = email.InvitedByName,
            AcceptLink = email.AcceptLink,
            ExpiresAt = email.ExpiresAt,
        };

        var message = await AccountEmailBuilder.BuildInvitationAsync(email.RecipientEmail, model, EmailCultureResolver.Resolve(email.Language));
        await EmailDispatcher.DispatchAsync(message, cancellationToken);
        return true;
    }
}
