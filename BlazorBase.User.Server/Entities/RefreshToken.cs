using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// A hashed, rotatable refresh-token record backing <see cref="Services.AuthTokenIssuer{TUser}"/>'s
/// rotation-with-reuse-detection scheme (OAuth 2.0 Security BCP for public clients). Only the hash of
/// the token value is stored, never the value itself, so a database read alone cannot mint a usable
/// token.
/// </summary>
[Index(nameof(Token), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(FamilyId))]
public class RefreshToken
{
    public Guid Id { get; set; }

    [Required, MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Identifies one rotation lineage. Every token minted by rotating an earlier one keeps the same
    /// family id, so replaying an already-rotated token revokes every token descended from the same
    /// login instead of only the one presented - the reuse-detection signal that a token was stolen.
    /// </summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// The user's security stamp captured at issuance time, re-checked with
    /// <see cref="Microsoft.AspNetCore.Identity.SignInManager{TUser}.ValidateSecurityStampAsync"/> on
    /// every refresh, so a stamp rotated elsewhere (password change, sign-out-everywhere) invalidates
    /// this token even though its own row is untouched by that rotation.
    /// </summary>
    [Required, MaxLength(200)]
    public string SecurityStamp { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}
