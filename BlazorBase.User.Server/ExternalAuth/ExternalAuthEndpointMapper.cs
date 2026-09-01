using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.Services;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Maps the external-token exchange endpoint as a minimal API - the framework's CRUD surface
/// (<c>MapBaseEndpoints</c>, <c>MapBlazorBaseCrudEndpoints</c>) already is minimal API, and a host
/// with no MVC stack at all must not be forced to add one just to reach this feature.
/// </summary>
public static class ExternalAuthEndpointMapper
{
    /// <summary>
    /// Maps <c>POST {path}/exchange</c>, rate-limited by the "BlazorBaseAuth" policy registered by
    /// <c>AddBlazorBaseUserServer</c>. Validates the external token via the registered
    /// <see cref="IExternalTokenValidator"/>, resolves the user via the registered
    /// <see cref="IExternalUserResolver{TUser}"/>, and returns the same <c>LoginResponse</c> shape
    /// the interactive login/setup/refresh endpoints return.
    /// </summary>
    public static IEndpointRouteBuilder MapBlazorBaseExternalAuthEndpoints<TUser>(
        this IEndpointRouteBuilder endpoints,
        string path = "api/auth/external")
        where TUser : BaseUser, new()
    {
        endpoints.MapPost($"{path}/exchange", async (
                HttpContext httpContext,
                ExternalTokenExchangeRequest request,
                IExternalTokenValidator tokenValidator,
                IExternalUserResolver<TUser> userResolver,
                UserManager<TUser> userManager,
                SignInManager<TUser> signInManager,
                TokenService<TUser> tokenService,
                BaseUserDbContext<TUser> dbContext,
                IAuthTokenTransport tokenTransport,
                CancellationToken cancellationToken) =>
            {
                if (string.IsNullOrWhiteSpace(request.ExternalToken))
                    return Results.Unauthorized();

                var externalPrincipal = await tokenValidator.ValidateAsync(request.ExternalToken, cancellationToken);
                if (externalPrincipal is null)
                    return Results.Unauthorized();

                var resolution = await userResolver.ResolveAsync(externalPrincipal, cancellationToken);
                if (!resolution.IsAccepted || resolution.User is null)
                    return Results.Unauthorized();

                var roles = await userManager.GetRolesAsync(resolution.User);
                var tokens = await tokenService.IssueTokenPairAsync(resolution.User, roles, dbContext);
                var principal = await signInManager.CreateUserPrincipalAsync(resolution.User);
                var response = await tokenTransport.WriteAsync(httpContext, tokens, principal, cancellationToken);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .RequireRateLimiting("BlazorBaseAuth");

        return endpoints;
    }
}
