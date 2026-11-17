using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// The fields of an OAuth Client ID Metadata Document the server reads (RFC 7591 client metadata). The
/// secret and key fields are read only to refuse a document that carries them.
/// </summary>
public sealed class OAuthClientIdMetadataDocument
{
    [JsonPropertyName("client_id")]
    public string? ClientId { get; set; }

    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; set; }

    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; set; }

    [JsonPropertyName("response_types")]
    public List<string>? ResponseTypes { get; set; }

    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public List<string>? TokenEndpointAuthMethodsSupported { get; set; }

    [JsonPropertyName("client_secret")]
    public JsonElement? ClientSecret { get; set; }

    [JsonPropertyName("client_secret_expires_at")]
    public JsonElement? ClientSecretExpiresAt { get; set; }

    [JsonPropertyName("jwks")]
    public JsonElement? Jwks { get; set; }

    [JsonPropertyName("jwks_uri")]
    public JsonElement? JwksUri { get; set; }
}
