using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// One approval a user gave on the consent page: this client may act for this user with this scope,
/// optionally bound to one protected resource. Every authorization code, refresh token and OAuth
/// access token hangs off a grant, so revoking the grant ends all of them at once - which is what
/// "disconnect this app" means to the user.
/// </summary>
[Index(nameof(UserId))]
[Index(nameof(ClientId))]
public class OAuthGrant
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(OAuthClient.MaximumIdLength)]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>The space-separated scopes the user approved.</summary>
    [Required]
    [MaxLength(1000)]
    public string Scope { get; set; } = string.Empty;

    /// <summary>The RFC 8707 resource indicator the authorization was bound to, if the client named one.</summary>
    [MaxLength(2000)]
    public string? Resource { get; set; }

    /// <summary>The redirect URI the authorization was answered on.</summary>
    [Required]
    [MaxLength(2000)]
    public string RedirectUri { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    /// <summary>When tokens were last issued under this grant.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>When the user, the client or reuse detection revoked the grant; <see langword="null"/> while active.</summary>
    public DateTime? RevokedAt { get; set; }
}
