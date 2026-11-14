namespace BlazorBase.User.Models;

/// <summary>
/// What the anonymous accept page learns about a token (<c>GET api/auth/invitations/accept?token=</c>).
/// An unknown, expired, revoked or used token answers the same way - only <see cref="IsValid"/>
/// <see langword="false"/> - so the endpoint reveals nothing beyond the token itself.
/// </summary>
public class UserInvitationPreview
{
    public bool IsValid { get; set; }

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
