namespace BlazorBase.User.Models;

/// <summary>One invitation as the administration lists it (<c>GET api/auth/invitations</c>). Never carries the token.</summary>
public class UserInvitationSummary
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string? DisplayName { get; set; }

    public string Role { get; set; } = string.Empty;

    /// <summary>The application-defined context string the invitation was created with.</summary>
    public string? Context { get; set; }

    /// <summary>The language the invitation email is written in and the new account starts with.</summary>
    public string? Language { get; set; }

    public UserInvitationStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string? InvitedByUserId { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string? AcceptedUserId { get; set; }

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastSentAt { get; set; }

    public int SendCount { get; set; }
}
