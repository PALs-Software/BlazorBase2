namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>The token-endpoint client authentication methods this server supports.</summary>
public static class OAuthClientAuthenticationMethods
{
    /// <summary>A public client: it sends only <c>client_id</c> and proves possession through PKCE.</summary>
    public const string None = "none";

    /// <summary>A confidential client sending <c>client_id</c> and <c>client_secret</c> as form fields.</summary>
    public const string ClientSecretPost = "client_secret_post";

    /// <summary>A confidential client sending its credentials in an HTTP Basic <c>Authorization</c> header.</summary>
    public const string ClientSecretBasic = "client_secret_basic";

    /// <summary>Every supported method, in the order the metadata document lists them.</summary>
    public static readonly IReadOnlyList<string> All = [None, ClientSecretPost, ClientSecretBasic];

    /// <summary>Returns <see langword="true"/> for the methods that require a client secret.</summary>
    public static bool UsesSecret(string method) => method is ClientSecretPost or ClientSecretBasic;
}
