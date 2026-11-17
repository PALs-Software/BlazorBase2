namespace BlazorBase.User.Server.Invitations.Mail;

/// <summary>Everything an <see cref="IUserInvitationEmailSender"/> needs to write one invitation email.</summary>
public sealed class UserInvitationEmail
{
    public required string RecipientEmail { get; init; }

    public string? RecipientName { get; init; }

    public string? InvitedByName { get; init; }

    public required string AcceptLink { get; init; }

    public DateTime ExpiresAt { get; init; }

    /// <summary>The language of the email, for example <c>de</c>; the ambient culture when empty.</summary>
    public string? Language { get; init; }
}
