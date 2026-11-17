using BlazorBase.User.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Authorization;

/// <summary>
/// The non-generic face of <see cref="OAuthAuthorizationEndpointHandler{TUser}"/>, so the endpoints can
/// be mapped without naming the host's user type.
/// </summary>
internal interface IOAuthAuthorizationEndpointHandler
{
    /// <summary><c>GET authorize</c>: validates the request and redirects to the consent page.</summary>
    Task<IResult> AuthorizeAsync(HttpContext httpContext);

    /// <summary><c>GET authorization-requests</c>: what the consent page shows about a pending request.</summary>
    Task<IResult> DescribeAsync(HttpContext httpContext, string? protectedRequest);

    /// <summary><c>POST authorization-requests/decision</c>: records the user's decision and returns the client redirect.</summary>
    Task<IResult> DecideAsync(HttpContext httpContext, OAuthAuthorizationDecisionRequest decision);
}
