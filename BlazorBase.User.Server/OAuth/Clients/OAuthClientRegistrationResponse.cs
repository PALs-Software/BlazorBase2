using System.Text.Json.Serialization;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The RFC 7591 registration response: the stored client metadata plus, for a confidential client, the
/// one-time plain-text <see cref="ClientSecret"/> - only its hash is kept.
/// </summary>
public sealed class OAuthClientRegistrationResponse
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Seconds since the Unix epoch.</summary>
    [JsonPropertyName("client_id_issued_at")]
    public long ClientIdIssuedAt { get; set; }

    [JsonPropertyName("client_name")]
    public string ClientName { get; set; } = string.Empty;

    [JsonPropertyName("redirect_uris")]
    public List<string> RedirectUris { get; set; } = [];

    [JsonPropertyName("grant_types")]
    public List<string> GrantTypes { get; set; } = [];

    [JsonPropertyName("response_types")]
    public List<string> ResponseTypes { get; set; } = [];

    [JsonPropertyName("token_endpoint_auth_method")]
    public string TokenEndpointAuthMethod { get; set; } = string.Empty;

    /// <summary>The client secret, issued only to <c>client_secret_post</c> and <c>client_secret_basic</c> clients.</summary>
    [JsonPropertyName("client_secret")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientSecret { get; set; }

    /// <summary><c>0</c> - the secret never expires - whenever a secret is issued.</summary>
    [JsonPropertyName("client_secret_expires_at")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ClientSecretExpiresAt { get; set; }
}
