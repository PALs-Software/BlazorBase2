namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>The grant types this server supports.</summary>
public static class OAuthGrantTypes
{
    public const string AuthorizationCode = "authorization_code";
    public const string RefreshToken = "refresh_token";

    /// <summary>Every supported grant type, in the order the metadata document lists them.</summary>
    public static readonly IReadOnlyList<string> All = [AuthorizationCode, RefreshToken];
}
