using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

/// <summary>The body of <c>POST api/auth/invitations</c>.</summary>
public class CreateUserInvitationRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Suggested display name; the invited person can change it when accepting.</summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [Required]
    [MaxLength(256)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// An application-defined value the invitation carries to the application's
    /// <c>IUserInvitationHandler</c>, for example the household the person joins.
    /// </summary>
    [MaxLength(2000)]
    public string? Context { get; set; }

    /// <summary>
    /// The language of the invitation email and of the new account (for example <c>de</c>); the user
    /// type's default language when empty.
    /// </summary>
    [MaxLength(10)]
    public string? Language { get; set; }
}
