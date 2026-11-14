namespace BlazorBase.Mailing.Models;

/// <summary>The values of the standard invitation email rendered by <c>InvitationEmail</c>.</summary>
public class InvitationEmailModel
{
    /// <summary>The name the invited person is greeted with; a neutral greeting is used when empty.</summary>
    public string? RecipientName { get; set; }

    /// <summary>The display name of the person who sent the invitation, if known.</summary>
    public string? InvitedByName { get; set; }

    /// <summary>The single-use link to the accept page.</summary>
    public string AcceptLink { get; set; } = string.Empty;

    /// <summary>When the link stops working, in UTC.</summary>
    public DateTime ExpiresAt { get; set; }
}
