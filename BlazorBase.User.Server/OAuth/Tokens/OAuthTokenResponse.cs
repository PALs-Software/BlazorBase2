using System.Text.Json.Serialization;

namespace BlazorBase.User.Server.OAuth.Tokens;

/// <summary>The RFC 6749 section 5.1 token response.</summary>
public sealed class OAuthTokenResponse
{
    /// <summary>An ordinary access-token secret, accepted by the access-token authentication scheme.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>Lifetime of <see cref="AccessToken"/> in seconds.</summary>
    [JsonPropertyName("expires_in")]
    public long ExpiresIn { get; set; }

    /// <summary>The rotating refresh token; only issued to a client registered for the <c>refresh_token</c> grant.</summary>
    [JsonPropertyName("refresh_token")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("scope")]
    public string Scope { get; set; } = string.Empty;
}
