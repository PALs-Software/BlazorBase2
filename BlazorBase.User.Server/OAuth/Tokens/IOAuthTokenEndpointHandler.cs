using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Tokens;

/// <summary>
/// The non-generic face of <see cref="OAuthTokenEndpointHandler{TUser}"/>, so the endpoints can be
/// mapped without naming the host's user type.
/// </summary>
internal interface IOAuthTokenEndpointHandler
{
    /// <summary><c>POST token</c>: the <c>authorization_code</c> and <c>refresh_token</c> grants.</summary>
    Task<IResult> TokenAsync(HttpContext httpContext);

    /// <summary><c>POST revoke</c>: RFC 7009 token revocation.</summary>
    Task<IResult> RevokeAsync(HttpContext httpContext);
}
