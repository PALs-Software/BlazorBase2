using System.Text.Json.Serialization;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The client metadata of an RFC 7591 dynamic client registration request. Members this server does
/// not use (<c>logo_uri</c>, <c>scope</c>, <c>contacts</c>, ...) are ignored.
/// </summary>
public sealed class OAuthClientRegistrationRequest
{
    /// <summary>Required: the exact redirect URIs authorization requests may name (at most ten).</summary>
    [JsonPropertyName("redirect_uris")]
    public List<string>? RedirectUris { get; set; }

    /// <summary>The display name; self-asserted, so the consent page shows the redirect host next to it.</summary>
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    /// <summary><c>none</c>, <c>client_secret_post</c> or <c>client_secret_basic</c> (the RFC 7591 default).</summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; }

    /// <summary>A subset of <c>authorization_code</c> and <c>refresh_token</c>; defaults to <c>authorization_code</c>.</summary>
    [JsonPropertyName("grant_types")]
    public List<string>? GrantTypes { get; set; }

    /// <summary>Only <c>code</c> is supported, which is also the default.</summary>
    [JsonPropertyName("response_types")]
    public List<string>? ResponseTypes { get; set; }
}
