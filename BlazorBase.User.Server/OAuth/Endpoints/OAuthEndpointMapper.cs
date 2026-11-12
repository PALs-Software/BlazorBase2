using System.Security.Claims;
using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.OAuth.Authorization;
using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Grants;
using BlazorBase.User.Server.OAuth.Metadata;
using BlazorBase.User.Server.OAuth.Protocol;
using BlazorBase.User.Server.OAuth.Tokens;
using BlazorBase.User.Server.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Endpoints;

/// <summary>
/// Maps the OAuth authorization server: the discovery documents, the flow endpoints under
/// <see cref="OAuthServerOptions.EndpointBasePath"/>, the consent API and the connected-apps API.
/// </summary>
public static class OAuthEndpointMapper
{
    /// <summary>The rate-limiter policy registered by <c>AddBlazorBaseUserServer</c>.</summary>
    private const string RateLimitPolicy = "BlazorBaseAuth";

    /// <summary>
    /// Maps every endpoint of the authorization server; call once, after
    /// <c>AddBlazorBaseOAuthServer</c>. Protected resources are added separately with
    /// <see cref="MapBlazorBaseOAuthProtectedResource"/>.
    /// </summary>
    /// <remarks>
    /// <c>register</c>, <c>token</c> and <c>revoke</c> are called server-to-server and carry their own
    /// client authentication, so they opt out of the CSRF middleware; the consent decision and grant
    /// revocation are cookie-authenticated under the cookie transport and keep it. <c>register</c>,
    /// <c>authorize</c>, <c>token</c> and <c>revoke</c> use the "BlazorBaseAuth" rate-limit policy.
    /// </remarks>
    /// <exception cref="InvalidOperationException"><c>AddBlazorBaseOAuthServer</c> was not called.</exception>
    public static IEndpointRouteBuilder MapBlazorBaseOAuthServer(this IEndpointRouteBuilder endpoints)
    {
        EnsureRegistered(endpoints);

        var options = endpoints.ServiceProvider.GetRequiredService<IOptions<OAuthServerOptions>>().Value;

        MapMetadataEndpoint(endpoints, OAuthMetadataDocumentBuilder.AuthorizationServerMetadataPath, BuildAuthorizationServerMetadata);
        MapMetadataEndpoint(endpoints, OAuthMetadataDocumentBuilder.ProtectedResourceMetadataPath, BuildDefaultProtectedResourceMetadata);

        var group = endpoints.MapGroup(options.NormalizedEndpointBasePath).AddEndpointFilter<OAuthNoStoreEndpointFilter>();

        if (options.EnableDynamicClientRegistration)
            group.MapPost("register", RegisterClientAsync).AllowAnonymous().AllowBlazorBaseCsrf().RequireRateLimiting(RateLimitPolicy);

        Func<HttpContext, Task<IResult>> authorize = httpContext => AuthorizationHandler(httpContext).AuthorizeAsync(httpContext);
        Func<HttpContext, Task<IResult>> token = httpContext => TokenHandler(httpContext).TokenAsync(httpContext);
        Func<HttpContext, Task<IResult>> revoke = httpContext => TokenHandler(httpContext).RevokeAsync(httpContext);

        group.MapGet("authorize", authorize)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("token", token)
            .AllowAnonymous()
            .AllowBlazorBaseCsrf()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapPost("revoke", revoke)
            .AllowAnonymous()
            .AllowBlazorBaseCsrf()
            .RequireRateLimiting(RateLimitPolicy);

        group.MapGet("authorization-requests", (HttpContext httpContext, string? request) => AuthorizationHandler(httpContext).DescribeAsync(httpContext, request))
            .RequireAuthorization();

        group.MapPost("authorization-requests/decision", (HttpContext httpContext, OAuthAuthorizationDecisionRequest decision) => AuthorizationHandler(httpContext).DecideAsync(httpContext, decision))
            .RequireAuthorization();

        group.MapGet("grants", ListGrantsAsync).RequireAuthorization();
        group.MapDelete("grants/{grantId:guid}", RevokeGrantAsync).RequireAuthorization();

        return endpoints;
    }

    /// <summary>
    /// Publishes the RFC 9728 metadata of the endpoint at <paramref name="resourcePath"/> (relative to the
    /// path base, for example <c>/api/mcp</c>) under <c>/.well-known/oauth-protected-resource{resourcePath}</c>,
    /// and makes the access-token challenge of every request at or below that path carry
    /// <c>resource_metadata</c>. The endpoint itself keeps its own
    /// <c>RequireAuthorization(AccessTokenAuthenticationDefaults.AuthenticationScheme)</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException"><c>AddBlazorBaseOAuthServer</c> was not called.</exception>
    public static IEndpointRouteBuilder MapBlazorBaseOAuthProtectedResource(this IEndpointRouteBuilder endpoints, string resourcePath)
    {
        EnsureRegistered(endpoints);

        var registry = endpoints.ServiceProvider.GetRequiredService<OAuthProtectedResourceRegistry>();
        var normalizedPath = registry.Register(resourcePath);

        MapMetadataEndpoint(
            endpoints,
            OAuthMetadataDocumentBuilder.ProtectedResourceMetadataPath + normalizedPath,
            httpContext => httpContext.RequestServices.GetRequiredService<OAuthMetadataDocumentBuilder>().BuildProtectedResourceMetadata(httpContext.Request, normalizedPath));

        return endpoints;
    }

    private static void EnsureRegistered(IEndpointRouteBuilder endpoints)
    {
        if (endpoints.ServiceProvider.GetService<OAuthProtectedResourceRegistry>() is null)
            throw new InvalidOperationException("The OAuth authorization server endpoints require services.AddBlazorBaseOAuthServer<TUser, TContext>() to have been called.");
    }

    /// <summary>
    /// Maps a public discovery document with <c>Access-Control-Allow-Origin: *</c> - it holds nothing
    /// secret, and browser-based MCP clients and inspectors read it cross-origin - plus the matching
    /// CORS preflight answer.
    /// </summary>
    private static void MapMetadataEndpoint(IEndpointRouteBuilder endpoints, string path, Func<HttpContext, Dictionary<string, object>?> buildDocument)
    {
        endpoints.MapGet(path, (HttpContext httpContext) =>
            {
                AllowAnyOrigin(httpContext);
                var document = buildDocument(httpContext);
                return document is null ? Results.NotFound() : Results.Json(document);
            })
            .AllowAnonymous();

        endpoints.MapMethods(path, [HttpMethods.Options], (HttpContext httpContext) =>
            {
                AllowAnyOrigin(httpContext);
                httpContext.Response.Headers.AccessControlAllowMethods = "GET, OPTIONS";
                httpContext.Response.Headers.AccessControlAllowHeaders = "*";
                return Results.NoContent();
            })
            .AllowAnonymous();
    }

    private static void AllowAnyOrigin(HttpContext httpContext) =>
        httpContext.Response.Headers.AccessControlAllowOrigin = "*";

    private static Dictionary<string, object>? BuildAuthorizationServerMetadata(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<OAuthMetadataDocumentBuilder>().BuildAuthorizationServerMetadata(httpContext.Request);

    /// <summary>The bare well-known path answers for the first registered resource, or 404 when there is none.</summary>
    private static Dictionary<string, object>? BuildDefaultProtectedResourceMetadata(HttpContext httpContext)
    {
        var resourcePath = httpContext.RequestServices.GetRequiredService<OAuthProtectedResourceRegistry>().ResourcePaths.FirstOrDefault();

        if (resourcePath is null)
            return null;

        return httpContext.RequestServices.GetRequiredService<OAuthMetadataDocumentBuilder>().BuildProtectedResourceMetadata(httpContext.Request, resourcePath);
    }

    private static async Task<IResult> RegisterClientAsync(HttpContext httpContext, IOAuthClientService clientService)
    {
        OAuthClientRegistrationRequest? registrationRequest;

        try
        {
            registrationRequest = await JsonSerializer.DeserializeAsync<OAuthClientRegistrationRequest>(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            registrationRequest = null;
        }

        if (registrationRequest is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidClientMetadata, "The request body must be a JSON object of client metadata.");

        var result = await clientService.RegisterAsync(registrationRequest, httpContext.RequestAborted);

        if (!result.Succeeded)
            return OAuthErrorResults.Error(httpContext, result.Error!);

        return Results.Json(result.Client, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListGrantsAsync(HttpContext httpContext, IOAuthGrantService grantService)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return Results.Ok(await grantService.ListAsync(userId, httpContext.RequestAborted));
    }

    private static async Task<IResult> RevokeGrantAsync(HttpContext httpContext, Guid grantId, IOAuthGrantService grantService)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        return await grantService.RevokeAsync(grantId, userId, httpContext.RequestAborted)
            ? Results.NoContent()
            : Results.NotFound();
    }

    private static IOAuthAuthorizationEndpointHandler AuthorizationHandler(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOAuthAuthorizationEndpointHandler>();

    private static IOAuthTokenEndpointHandler TokenHandler(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<IOAuthTokenEndpointHandler>();
}
