using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// A rotating OAuth refresh token. Only the SHA-256 hash is stored, and every redemption consumes the
/// row and issues a successor. A consumed token presented again after the reuse grace period revokes
/// the whole <see cref="OAuthGrant"/> - the reuse-detection signal that a token was stolen.
/// </summary>
[Index(nameof(TokenHash), IsUnique = true)]
[Index(nameof(GrantId))]
public class OAuthRefreshToken
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>The Base64Url SHA-256 hash of the token.</summary>
    /// <remarks>Excluded from every generated endpoint: hashed or not, it is credential material.</remarks>
    [CrudAccess("*", "")]
    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public Guid GrantId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>When the token was redeemed or its grant revoked; <see langword="null"/> while usable.</summary>
    public DateTime? ConsumedAt { get; set; }
}
