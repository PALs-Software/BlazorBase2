using System.Security.Claims;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Maps <c>GET {path}/session</c>, which echoes the calling principal's claims and hands out the
/// antiforgery request token.
/// </summary>
/// <remarks>
/// Required by the cookie transport and harmless with any other: a client whose token is
/// <c>HttpOnly</c> cannot decode its own claims, so without this it has nothing to drive its UI with.
///
/// It is an echo, not a grant. The response contains only what the caller already authenticated as,
/// and an unauthenticated caller gets <c>IsAuthenticated = false</c> rather than a challenge — a
/// client needs to be able to ask "am I signed in?" without being redirected for asking. A cross-site
/// page cannot read the answer, because there is no CORS policy that would let it.
///
/// Issuing the antiforgery token here rather than at sign-in is deliberate on both counts. The token
/// is bound to the caller's identity, so it must be minted on a request where that identity already
/// exists; and a client asks this endpoint on every start, which is what keeps the token alive across
/// a browser restart.
/// </remarks>
public static class AuthSessionEndpointMapper
{
    public static IEndpointRouteBuilder MapBlazorBaseAuthSessionEndpoint(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth")
    {
        endpoints.ServiceProvider.GetService<BlazorBaseAuthSessionEndpointRegistration>()?.WasRegistered = true;

        endpoints.MapGet($"{path}/session", (HttpContext httpContext, IOptions<IdentityOptions> identityOptions) =>
            {
                var user = httpContext.User;

                // Absent in a bearer-only application, which registers no antiforgery and needs none:
                // a token the caller attaches deliberately is already proof of intent.
                var antiforgery = httpContext.RequestServices.GetService<IAntiforgery>();
                var csrfToken = antiforgery?.GetAndStoreTokens(httpContext).RequestToken ?? string.Empty;

                if (user.Identity?.IsAuthenticated != true)
                    return Results.Ok(new AuthSessionResponse { CsrfToken = csrfToken });

                var securityStampClaimType = identityOptions.Value.ClaimsIdentity.SecurityStampClaimType;

                return Results.Ok(new AuthSessionResponse
                {
                    IsAuthenticated = true,
                    CsrfToken       = csrfToken,
                    Claims          = [.. user.Claims
                        .Where(claim => IsClientVisibleClaim(claim, securityStampClaimType))
                        .Select(claim => new AuthSessionClaim(claim.Type, claim.Value))],
                });
            })
            .AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// The security stamp is internal bookkeeping - it names the validity generation of an account
    /// and the server compares against it - so it has no client-side use and is not handed out. Its
    /// claim type is host-configurable, hence the parameter rather than a constant.
    /// </summary>
    private static bool IsClientVisibleClaim(Claim claim, string securityStampClaimType) =>
        claim.Type != securityStampClaimType;
}
