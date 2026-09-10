using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
/// All this adds is the conversion step: read back the principal the external handler signed into
/// <c>signInScheme</c> - a remote handler such as <c>AddOpenIdConnect</c> never authenticates its own
/// scheme outside of the callback request it is currently processing - resolve it to an application
/// user through the same <see cref="IExternalUserResolver{TUser}"/> the token exchange uses, and hand
/// the result to the configured <see cref="IAuthTokenTransport"/>.
///
/// Pair with <c>AddBlazorBaseCookieAuthTokens</c>. With the default bearer transport these endpoints
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
    /// <param name="signInScheme">
    /// The scheme <paramref name="externalScheme"/>'s handler actually signs into - what
    /// <c>AddMicrosoftIdentityWebApp</c> and <c>AddOpenIdConnect</c> call <c>SignInScheme</c>. The
    /// callback for <paramref name="externalScheme"/> only ever produces a principal while it is
    /// processing its own callback request, so <c>complete</c> reads the principal back from
    /// <paramref name="signInScheme"/> instead, and signs out of it once the framework session takes
    /// over. Defaults to <see cref="CookieAuthenticationDefaults.AuthenticationScheme"/>, the value
    /// both of those calls use unless told otherwise. Signing out of <paramref name="externalScheme"/>
    /// itself would be wrong: for an OIDC handler that triggers a redirect to the provider's own
    /// <c>end_session_endpoint</c> - a global sign-out at the identity provider, not the local
    /// cleanup this method needs.
    /// </param>
    /// <param name="defaultReturnUrl">Where to land when the caller supplies no return URL.</param>
    public static IEndpointRouteBuilder MapBlazorBaseExternalAuthRedirectEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string externalScheme,
        string signInScheme = CookieAuthenticationDefaults.AuthenticationScheme,
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
                SignInManager<TUser> signInManager,
                AuthTokenIssuer<TUser> tokenIssuer,
                IAuthTokenTransport tokenTransport,
                CancellationToken cancellationToken) =>
            {
                var authenticateResult = await httpContext.AuthenticateAsync(signInScheme);

                if (!authenticateResult.Succeeded || authenticateResult.Principal is null)
                    return Results.Unauthorized();

                var resolution = await userResolver.ResolveAsync(authenticateResult.Principal, cancellationToken);

                if (!resolution.IsAccepted || resolution.User is null)
                    return await RejectAndSignOutAsync(httpContext, signInScheme);

                if (!await ExternalAuthEndpointMapper.IsAccountActiveAndNotLockedOutAsync(signInManager, resolution.User))
                    return await RejectAndSignOutAsync(httpContext, signInScheme);

                var principal = await signInManager.CreateUserPrincipalAsync(resolution.User);
                var tokens = await tokenIssuer.IssueTokenPairAsync(principal, cancellationToken: cancellationToken);

                await tokenTransport.WriteAsync(httpContext, tokens, principal, cancellationToken);
                await SignOutOfIntermediateSignInAsync(httpContext, signInScheme);

                return Results.LocalRedirect(ResolveLocalReturnUrl(returnUrl, defaultReturnUrl));
            })
            .AllowAnonymous()
            .RequireRateLimiting("BlazorBaseAuth");

        return endpoints;
    }

    /// <summary>
    /// Signs out of <paramref name="signInScheme"/> and answers 401 - safe to call before answering
    /// since nothing on either rejection path has written a redirect yet.
    /// </summary>
    private static async Task<IResult> RejectAndSignOutAsync(HttpContext httpContext, string signInScheme)
    {
        await httpContext.SignOutAsync(signInScheme);
        return Results.Unauthorized();
    }

    /// <summary>
    /// Drops the intermediate external sign-in cookie now that the framework session exists. Must
    /// sign out of <paramref name="signInScheme"/>, never the external scheme itself: for an OIDC
    /// handler that would redirect to the provider's own <c>end_session_endpoint</c>, overwriting the
    /// <c>LocalRedirect</c> this method's caller issues right after and doing neither.
    /// </summary>
    private static Task SignOutOfIntermediateSignInAsync(HttpContext httpContext, string signInScheme) =>
        httpContext.SignOutAsync(signInScheme);

    /// <summary>
    /// Accepts only a same-application path. A return URL that survives to a redirect unchecked is an
    /// open redirect, and one that arrives right after a successful sign-in is the most convincing
    /// possible phishing hop. Malformed input - a control character, or leading/trailing whitespace a
    /// browser's URL parser silently strips - is rejected outright rather than normalised, so what
    /// this method inspects is exactly what a browser would end up navigating to.
    /// </summary>
    private static string ResolveLocalReturnUrl(string? returnUrl, string defaultReturnUrl)
    {
        if (string.IsNullOrEmpty(returnUrl))
            return defaultReturnUrl;

        if (returnUrl.Any(char.IsControl) || returnUrl != returnUrl.Trim())
            return defaultReturnUrl;

        if (!returnUrl.StartsWith('/') || IsProtocolRelative(returnUrl))
            return defaultReturnUrl;

        return returnUrl;
    }

    /// <summary>"//host" and "/\host" are protocol-relative and leave the application despite the leading slash.</summary>
    private static bool IsProtocolRelative(string returnUrl) =>
        returnUrl.StartsWith("//", StringComparison.Ordinal) || returnUrl.StartsWith("/\\", StringComparison.Ordinal);
}
