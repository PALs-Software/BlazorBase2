using System.ComponentModel.DataAnnotations;
using BlazorBase.CRUD.Attributes;

namespace BlazorBase.User.Server.Entities;

/// <summary>
/// An application known to the OAuth authorization server: registered through dynamic client
/// registration (RFC 7591) or by the host through <c>IOAuthClientService.RegisterAsync</c>, or loaded from
/// the OAuth Client ID Metadata Document its <c>https</c> client id points at (a claude.ai connector, for
/// example), in which case the row caches that document until <see cref="MetadataDocumentExpiresAt"/>.
/// </summary>
/// <remarks>
/// <see cref="ClientName"/> is asserted by the client itself and proves nothing about who operates it,
/// which is why the consent page shows the redirect host next to it. A confidential client's secret is
/// stored only as its SHA-256 hash; the plain value exists once, in the registration response.
/// </remarks>
public class OAuthClient
{
    /// <summary>The longest client id accepted, including a client id metadata document URL.</summary>
    public const int MaximumIdLength = 400;

    /// <summary>
    /// The public client identifier: <c>bbc_</c> followed by 22 random Base64Url characters for a
    /// registered client, the metadata document URL for a <see cref="IsMetadataDocumentClient"/>.
    /// </summary>
    [Key]
    [MaxLength(MaximumIdLength)]
    public string Id { get; set; } = string.Empty;

    /// <summary>The display name the client registered with - self-asserted, never proof of identity.</summary>
    [Required]
    [MaxLength(200)]
    public string ClientName { get; set; } = "Unnamed client";

    /// <summary>The exact redirect URIs an authorization request may name; compared by ordinal string equality.</summary>
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// The grant types the client registered for (<c>authorization_code</c>, optionally
    /// <c>refresh_token</c>). A refresh token is only issued to a client that registered for it.
    /// </summary>
    public List<string> GrantTypes { get; set; } = [];

    /// <summary><c>none</c>, <c>client_secret_post</c> or <c>client_secret_basic</c>.</summary>
    [Required]
    [MaxLength(32)]
    public string TokenEndpointAuthMethod { get; set; } = string.Empty;

    /// <summary>The Base64Url SHA-256 hash of the client secret, or <see langword="null"/> for a public client.</summary>
    /// <remarks>Excluded from every generated endpoint: hashed or not, it is credential material.</remarks>
    [CrudAccess("*", "")]
    [MaxLength(128)]
    public string? ClientSecretHash { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the client last redeemed a code or refresh token.</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// <see langword="true"/> when the client was loaded from the Client ID Metadata Document at
    /// <see cref="Id"/> rather than registered.
    /// </summary>
    public bool IsMetadataDocumentClient { get; set; }

    /// <summary>When the cached metadata document must be fetched again; only set for a metadata document client.</summary>
    public DateTime? MetadataDocumentExpiresAt { get; set; }

    /// <summary>When the metadata document was last fetched successfully; bounds how long a stale copy may be used.</summary>
    public DateTime? MetadataDocumentFetchedAt { get; set; }
}
