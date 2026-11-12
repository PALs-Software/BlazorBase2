namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>The OAuth error codes this server answers with (RFC 6749, RFC 7591, RFC 8707).</summary>
public static class OAuthErrorCodes
{
    public const string InvalidRequest = "invalid_request";
    public const string InvalidClient = "invalid_client";
    public const string InvalidGrant = "invalid_grant";
    public const string UnauthorizedClient = "unauthorized_client";
    public const string UnsupportedGrantType = "unsupported_grant_type";
    public const string UnsupportedResponseType = "unsupported_response_type";
    public const string InvalidScope = "invalid_scope";
    public const string InvalidTarget = "invalid_target";
    public const string AccessDenied = "access_denied";
    public const string InvalidRedirectUri = "invalid_redirect_uri";
    public const string InvalidClientMetadata = "invalid_client_metadata";
}
