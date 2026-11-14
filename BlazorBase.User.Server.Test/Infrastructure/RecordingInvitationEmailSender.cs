using BlazorBase.User.Server.Invitations.Mail;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>Captures every invitation email instead of sending it; <see cref="Sends"/> decides what it reports.</summary>
public sealed class RecordingInvitationEmailSender : IUserInvitationEmailSender
{
    public List<UserInvitationEmail> Emails { get; } = [];

    public bool Sends { get; set; } = true;

    public Task<bool> SendAsync(UserInvitationEmail email, CancellationToken cancellationToken = default)
    {
        Emails.Add(email);
        return Task.FromResult(Sends);
    }
}
