using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// An administrator's invitation of one email address into the application with one role. The person
/// redeems it once through a link; only the SHA-256 hash of the link's token is stored, so the plain
/// token exists only in the email and in the response that created it. Creating, resending or renewing
/// replaces the hash, which makes every older link useless.
/// </summary>
[Index(nameof(NormalizedEmail))]
[Index(nameof(TokenHash), IsUnique = true)]
public class UserInvitation
{
    public const int MaximumContextLength = 2000;

    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>The Identity-normalized <see cref="Email"/>, the lookup key for duplicates.</summary>
    [Required]
    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>The suggested display name; the invited person may change it when accepting.</summary>
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [Required]
    [MaxLength(256)]
    public string Role { get; set; } = string.Empty;

    /// <summary>An application-defined value passed through to <c>IUserInvitationHandler</c>.</summary>
    [MaxLength(MaximumContextLength)]
    public string? Context { get; set; }

    /// <summary>The language of the email and of the new account; the user type's default when empty.</summary>
    [MaxLength(10)]
    public string? Language { get; set; }

    /// <summary>The Base64Url SHA-256 hash of the current token.</summary>
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public string? InvitedByUserId { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public string? AcceptedUserId { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>When the invitation email was last handed to the mail pipeline.</summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>How many invitation emails were handed to the mail pipeline.</summary>
    public int SendCount { get; set; }
}
