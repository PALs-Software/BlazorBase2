using System.Security.Claims;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Maps <c>GET {path}/session</c>, which echoes the calling principal's claims.
/// </summary>
/// <remarks>
/// Required by the cookie transport and harmless with any other: a client whose token is
/// <c>HttpOnly</c> cannot decode its own claims, so without this it has nothing to drive its UI with.
///
/// It is an echo, not a grant. The response contains only what the caller already authenticated as,
/// and an unauthenticated caller gets <c>IsAuthenticated = false</c> rather than a challenge — a
/// client needs to be able to ask "am I signed in?" without being redirected for asking.
/// </remarks>
public static class AuthSessionEndpointMapper
{
    public static IEndpointRouteBuilder MapBlazorBaseAuthSessionEndpoint(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth")
    {
        endpoints.MapGet($"{path}/session", (HttpContext httpContext) =>
            {
                var user = httpContext.User;

                if (user.Identity?.IsAuthenticated != true)
                    return Results.Ok(new AuthSessionResponse { IsAuthenticated = false });

                return Results.Ok(new AuthSessionResponse
                {
                    IsAuthenticated = true,
                    Claims = [.. user.Claims.Select(claim => new AuthSessionClaim(claim.Type, claim.Value))],
                });
            })
            .AllowAnonymous();

        return endpoints;
    }
}
