using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Server.Invitations.Mail;

/// <summary>
/// The <see cref="IUserInvitationEmailSender"/> of a host without mailing: sends nothing, logs it and
/// answers <see langword="false"/>, so the invitation is still created and the administrator copies the link.
/// </summary>
public sealed class NoOpUserInvitationEmailSender(ILogger<NoOpUserInvitationEmailSender> logger) : IUserInvitationEmailSender
{
    #region Injects
    private readonly ILogger<NoOpUserInvitationEmailSender> Logger = logger;
    #endregion

    public Task<bool> SendAsync(UserInvitationEmail email, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("No mail pipeline is configured; the invitation email was not sent and the link has to be passed on by hand.");
        return Task.FromResult(false);
    }
}
