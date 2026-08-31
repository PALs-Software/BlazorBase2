using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Converts a session established by a redirect-based external provider into a framework session —
/// the backend-for-frontend half of external authentication.
/// </summary>
/// <remarks>
/// The framework deliberately does not own the OIDC protocol. The application registers whatever
/// handler it needs (<c>AddMicrosoftIdentityWebApp</c>, <c>AddOpenIdConnect</c>, a social provider)
/// and keeps everything that comes with it — including <c>ITokenAcquisition</c> for calling a
/// downstream API on the user's behalf, which only works while those tokens stay on the server.
///
/// All this adds is the conversion step: take the principal the external handler produced, resolve it
/// to an application user through the same <see cref="IExternalUserResolver{TUser}"/> the token
/// exchange uses, and hand the result to the configured <see cref="IAuthTokenTransport"/>.
///
/// Pair with <c>AddBlazorBaseCookieAuthTokens</c>. With the default body transport these endpoints
/// would put the token in a redirect response, which is not useful — see the guard in
/// <c>Complete</c>.
/// </remarks>
public static class ExternalAuthRedirectEndpointMapper
{
    /// <summary>
    /// Maps <c>GET {path}/challenge</c> and <c>GET {path}/complete</c>.
    /// </summary>
    /// <param name="externalScheme">
    /// The authentication scheme the application registered for its provider, for example
    /// <c>OpenIdConnectDefaults.AuthenticationScheme</c>.
    /// </param>
    /// <param name="defaultReturnUrl">Where to land when the caller supplies no return URL.</param>
    public static IEndpointRouteBuilder MapBlazorBaseExternalAuthRedirectEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string externalScheme,
        string path = "api/auth/external",
        string defaultReturnUrl = "/")
        where TUser : BaseUser, new()
    {
        endpoints.MapGet($"{path}/challenge", (HttpContext httpContext, string? returnUrl) =>
            {
                var target = ResolveLocalReturnUrl(returnUrl, defaultReturnUrl);
                var completeUrl = $"/{path.TrimStart('/')}/complete?returnUrl={Uri.EscapeDataString(target)}";

                return Results.Challenge(
                    new AuthenticationProperties { RedirectUri = completeUrl },
                    [externalScheme]);
            })
            .AllowAnonymous()
            .RequireRateLimiting("BlazorBaseAuth");

        endpoints.MapGet($"{path}/complete", async (
                HttpContext httpContext,
                string? returnUrl,
                IExternalUserResolver<TUser> userResolver,
                UserManager<TUser> userManager,
                TokenService<TUser> tokenService,
                BaseUserDbContext<TUser> dbContext,
                IAuthTokenTransport tokenTransport,
                CancellationToken cancellationToken) =>
            {
                var authenticateResult = await httpContext.AuthenticateAsync(externalScheme);

                if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
                    return Results.Unauthorized();

                var resolution = await userResolver.ResolveAsync(authenticateResult.Principal, cancellationToken);

                if (!resolution.IsAccepted || resolution.User is null)
                {
                    await httpContext.SignOutAsync(externalScheme);
                    return Results.Unauthorized();
                }

                var roles  = await userManager.GetRolesAsync(resolution.User);
                var tokens = await tokenService.IssueTokenPairAsync(resolution.User, roles, dbContext);

                await tokenTransport.WriteAsync(httpContext, tokens, cancellationToken);

                // The external session has done its job; leaving it behind means two overlapping
                // sessions with different lifetimes and only one of them revocable.
                await httpContext.SignOutAsync(externalScheme);

                return Results.LocalRedirect(ResolveLocalReturnUrl(returnUrl, defaultReturnUrl));
            })
            .AllowAnonymous()
            .RequireRateLimiting("BlazorBaseAuth");

        return endpoints;
    }

    /// <summary>
    /// Accepts only a same-application path. A return URL that survives to a redirect unchecked is an
    /// open redirect, and one that arrives right after a successful sign-in is the most convincing
    /// possible phishing hop.
    /// </summary>
    private static string ResolveLocalReturnUrl(string? returnUrl, string defaultReturnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            return defaultReturnUrl;

        // "//host" and "/\host" are protocol-relative and leave the application despite the leading slash.
        if (!returnUrl.StartsWith('/') || returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith("/\\", StringComparison.Ordinal))
            return defaultReturnUrl;

        return returnUrl;
    }
}
