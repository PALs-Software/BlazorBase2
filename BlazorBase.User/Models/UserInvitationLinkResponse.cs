namespace BlazorBase.User.Models;

/// <summary>
/// The answer to creating, resending or renewing an invitation: the invitation, the new accept link and
/// whether an email went out. Every one of these calls creates a new token, so any older link stops working.
/// </summary>
public class UserInvitationLinkResponse
{
    public UserInvitationSummary Invitation { get; set; } = new();

    /// <summary>The single-use accept link; the plain token exists only in this response and in the email.</summary>
    public string Link { get; set; } = string.Empty;

    /// <summary>
    /// <see langword="false"/> when no mail sender is configured or the call did not send one: the
    /// administrator then passes <see cref="Link"/> on themselves.
    /// </summary>
    public bool EmailSent { get; set; }
}
