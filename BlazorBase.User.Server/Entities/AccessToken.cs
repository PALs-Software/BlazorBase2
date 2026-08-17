using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Core;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// A hashed, revocable, long-lived credential for programmatic access (MCP servers, REST clients,
/// CI jobs) - the machine-to-machine counterpart to the short-lived JWT a browser session carries.
/// Only the SHA-512 hash of the secret is ever stored; the plain-text secret exists exactly once,
/// in the return value of <c>IAccessTokenService.CreateAsync</c>.
/// </summary>
/// <remarks>
/// <see cref="UserId"/> is nullable on purpose, because the two shapes of consuming application
/// both have to fit: an app where every token belongs to the account that minted it (set the id,
/// then filter every list/revoke call by it), and an app whose data is a single shared collection
/// with no per-user ownership at all (leave it null). Making it required would have forced the
/// second kind to invent a synthetic owner; making it non-nullable later is a widening change,
/// while the reverse would not be.
/// </remarks>
[Index(nameof(Prefix))]
[Index(nameof(UserId))]
public class AccessToken : AuditModel
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The owning account, or <see langword="null"/> for a token that is not bound to one.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// The non-secret, indexed lookup key: the configured secret prefix plus a fixed number of
    /// leading characters of the secret body. Narrows a validation to a handful of candidate rows
    /// without the secret itself ever reaching a query.
    /// </summary>
    [Required]
    [MaxLength(32)]
    public string Prefix { get; set; } = string.Empty;

    /// <summary>The SHA-512 hash of the full secret, Base64-encoded.</summary>
    [Required]
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>An operator-chosen label, shown when listing tokens.</summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>When the token stops being accepted, or <see langword="null"/> for no expiry.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the token last authenticated a request, updated best-effort and throttled.</summary>
    public DateTime? LastUsedAt { get; set; }

    public bool IsRevoked { get; set; }
}
