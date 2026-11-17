using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// A short-lived, single-use authorization code issued when the user approves a request. Only the
/// SHA-256 hash of the code is stored. Redeeming it a second time revokes the whole
/// <see cref="OAuthGrant"/> (RFC 6749 section 4.1.2), because a replayed code means someone other
/// than the legitimate client holds it.
/// </summary>
[Index(nameof(CodeHash), IsUnique = true)]
public class OAuthAuthorizationCode
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>The Base64Url SHA-256 hash of the code.</summary>
    /// <remarks>Excluded from every generated endpoint: hashed or not, it is credential material.</remarks>
    [CrudAccess("*", "")]
    [Required]
    [MaxLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public Guid GrantId { get; set; }

    /// <summary>
    /// The <c>redirect_uri</c> exactly as the authorization request named it, or <see langword="null"/>
    /// when the request omitted it because the client has a single registered URI. A token request must
    /// repeat a named URI verbatim.
    /// </summary>
    [MaxLength(2000)]
    public string? RedirectUri { get; set; }

    /// <summary>The PKCE <c>S256</c> code challenge the token request's verifier must hash to.</summary>
    [Required]
    [MaxLength(128)]
    public string CodeChallenge { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>When the code was redeemed; set by a conditional update so two parallel redemptions cannot both win.</summary>
    public DateTime? ConsumedAt { get; set; }
}
