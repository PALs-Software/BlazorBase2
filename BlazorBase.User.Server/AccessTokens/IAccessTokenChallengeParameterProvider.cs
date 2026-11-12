using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// Optional seam that adds auth-params to the <c>WWW-Authenticate: Bearer</c> challenge
/// <see cref="AccessTokenAuthenticationHandler"/> answers an unauthenticated request with. Nothing is
/// registered by default; the OAuth authorization server registers one that points a client at the
/// RFC 9728 protected-resource metadata (<c>resource_metadata="..."</c>) for a protected path.
/// </summary>
/// <remarks>
/// Every registered implementation is asked, in registration order, and the parameters are appended
/// after the handler's own <c>error="invalid_token"</c>. Values are quoted and escaped by the handler.
/// </remarks>
public interface IAccessTokenChallengeParameterProvider
{
    /// <summary>
    /// Returns the auth-params to add for <paramref name="httpContext"/>'s request; an empty sequence
    /// adds nothing.
    /// </summary>
    IEnumerable<KeyValuePair<string, string>> GetParameters(HttpContext httpContext);
}
