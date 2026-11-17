namespace BlazorBase.User.Server.Invitations.Mail;

/// <summary>
/// Sends invitation emails. <c>AddBlazorBaseUserInvitations</c> picks
/// <see cref="MailingUserInvitationEmailSender"/> when <c>BlazorBase.Mailing</c> is registered and
/// <see cref="NoOpUserInvitationEmailSender"/> otherwise; a host may register its own implementation instead.
/// </summary>
public interface IUserInvitationEmailSender
{
    /// <summary>
    /// Hands the email to the mail pipeline. Returns <see langword="false"/> when nothing was sent, so the
    /// administrator is told to pass the link on themselves.
    /// </summary>
    Task<bool> SendAsync(UserInvitationEmail email, CancellationToken cancellationToken = default);
}
