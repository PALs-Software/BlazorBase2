using System.Security.Claims;
using BlazorBase.User.Models;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Endpoints;
using BlazorBase.User.Server.OAuth.Grants;
using BlazorBase.User.Server.OAuth.Metadata;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Authorization;

/// <summary>
/// The front channel of the authorization-code flow: <c>authorize</c> validates the client's request
/// and hands it, sealed, to the consent page; the consent page reads it back and posts the user's
/// decision, which creates the grant and the code.
/// </summary>
/// <remarks>
/// Until the client and the redirect URI are established, nothing redirects - a bad request is a plain
/// 400 - because redirecting to an unvalidated URI would make this server an open redirector. Every
/// later error goes back to the validated redirect URI in the RFC 6749 section 4.1.2.1 shape, with
/// <c>iss</c> (RFC 9207) so the client can tell which server answered.
/// </remarks>
internal sealed class OAuthAuthorizationEndpointHandler<TUser>(
    IOAuthClientService clientService,
    OAuthGrantService<TUser> grantService,
    OAuthAuthorizationRequestProtector requestProtector,
    OAuthIssuerResolver issuerResolver,
    OAuthProtectedResourceRegistry resourceRegistry,
    IOptionsMonitor<OAuthServerOptions> options) : IOAuthAuthorizationEndpointHandler
    where TUser : BaseUser
{
    #region Injects
    private readonly IOAuthClientService ClientService = clientService;
    private readonly OAuthGrantService<TUser> GrantService = grantService;
    private readonly OAuthAuthorizationRequestProtector RequestProtector = requestProtector;
    private readonly OAuthIssuerResolver IssuerResolver = issuerResolver;
    private readonly OAuthProtectedResourceRegistry ResourceRegistry = resourceRegistry;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    #endregion

    private const string CodeResponseType = "code";
    private const string InvalidRequestMessage = "The authorization request is invalid or has expired.";

    public async Task<IResult> AuthorizeAsync(HttpContext httpContext)
    {
        var query = httpContext.Request.Query;

        if (!OAuthParameterReader.TryReadSingle(query["client_id"], out var clientId) || clientId is null)
            return Reject("client_id is missing or repeated.");

        var resolution = await ClientService.ResolveAsync(clientId, httpContext.RequestAborted);

        if (resolution.Client is not { } client)
            return Reject(resolution.Error ?? "The client is unknown.");

        if (!OAuthParameterReader.TryReadSingle(query["redirect_uri"], out var requestedRedirectUri))
            return Reject("redirect_uri must not be repeated.");

        if (requestedRedirectUri is null && client.RedirectUris.Count != 1)
            return Reject("redirect_uri is required because the client registered more than one redirect URI.");

        if (requestedRedirectUri is not null && !client.RedirectUris.Any(registeredUri => OAuthRedirectUriMatcher.Matches(registeredUri, requestedRedirectUri)))
            return Reject("redirect_uri does not match a redirect URI registered for this client.");

        var redirectUri = requestedRedirectUri ?? client.RedirectUris[0];
        var issuer = IssuerResolver.Resolve(httpContext.Request);
        var stateReadable = OAuthParameterReader.TryReadSingle(query["state"], out var state);

        var parametersReadable = stateReadable
            & OAuthParameterReader.TryReadSingle(query["response_type"], out var responseType)
            & OAuthParameterReader.TryReadSingle(query["code_challenge"], out var codeChallenge)
            & OAuthParameterReader.TryReadSingle(query["code_challenge_method"], out var codeChallengeMethod)
            & OAuthParameterReader.TryReadSingle(query["scope"], out var scope)
            & OAuthParameterReader.TryReadSingle(query["resource"], out var resource);

        if (!parametersReadable)
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidRequest, "A request parameter was repeated.", state, issuer);

        if (responseType != CodeResponseType)
            return RedirectWithError(redirectUri, OAuthErrorCodes.UnsupportedResponseType, "Only response_type=code is supported.", state, issuer);

        if (!client.GrantTypes.Contains(OAuthGrantTypes.AuthorizationCode))
            return RedirectWithError(redirectUri, OAuthErrorCodes.UnauthorizedClient, "The client is not registered for the authorization_code grant.", state, issuer);

        if (codeChallenge is null)
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidRequest, "code_challenge is required (PKCE).", state, issuer);

        if (codeChallengeMethod != OAuthPkce.S256)
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidRequest, "code_challenge_method must be S256.", state, issuer);

        if (!OAuthPkce.IsValidChallenge(codeChallenge))
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidRequest, "code_challenge must be 43 to 128 Base64Url characters.", state, issuer);

        var configuredScopes = Options.CurrentValue.Scopes;
        var requestedScopes = OAuthScopes.Parse(scope);

        if (requestedScopes.Any(requestedScope => !configuredScopes.ContainsKey(requestedScope)))
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidScope, "A requested scope is not supported by this server.", state, issuer);

        var grantedScopes = requestedScopes.Count == 0 ? configuredScopes.Keys.ToList() : requestedScopes;

        if (resource is not null && !IsRegisteredResource(resource, issuer))
            return RedirectWithError(redirectUri, OAuthErrorCodes.InvalidTarget, "resource does not name a protected resource of this server.", state, issuer);

        var authorizationRequest = new OAuthAuthorizationRequest(
            client.Id,
            redirectUri,
            requestedRedirectUri is not null,
            codeChallenge,
            state,
            OAuthScopes.Join(grantedScopes),
            resource);

        var protectedRequest = RequestProtector.Protect(authorizationRequest);
        var consentLocation = $"{httpContext.Request.PathBase.ToUriComponent()}{Options.CurrentValue.ConsentPagePath}?request={Uri.EscapeDataString(protectedRequest)}";

        return Results.Redirect(consentLocation);
    }

    public async Task<IResult> DescribeAsync(HttpContext httpContext, string? protectedRequest)
    {
        var authorizationRequest = RequestProtector.Unprotect(protectedRequest);

        if (authorizationRequest is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, InvalidRequestMessage);

        if (ResolveApprovingUserId(httpContext.User) is null)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var client = await ClientService.FindAsync(authorizationRequest.ClientId, httpContext.RequestAborted);

        if (client is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, InvalidRequestMessage);

        var configuredScopes = Options.CurrentValue.Scopes;

        return Results.Ok(new OAuthAuthorizationRequestDetails
        {
            ClientName = client.ClientName,
            RedirectHost = OAuthRedirectHost.Describe(authorizationRequest.RedirectUri),
            ClientIdHost = client.IsMetadataDocumentClient ? OAuthRedirectHost.Describe(client.Id) : null,
            Scopes = OAuthScopes.Parse(authorizationRequest.Scope)
                .Select(scopeName => new OAuthScopeDetails { Name = scopeName, Description = configuredScopes.GetValueOrDefault(scopeName, string.Empty) })
                .ToList(),
        });
    }

    public async Task<IResult> DecideAsync(HttpContext httpContext, OAuthAuthorizationDecisionRequest decision)
    {
        var authorizationRequest = RequestProtector.Unprotect(decision.Request);

        if (authorizationRequest is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, InvalidRequestMessage);

        var userId = ResolveApprovingUserId(httpContext.User);

        if (userId is null)
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var client = await ClientService.FindAsync(authorizationRequest.ClientId, httpContext.RequestAborted);

        if (client is null || !client.RedirectUris.Any(registeredUri => OAuthRedirectUriMatcher.Matches(registeredUri, authorizationRequest.RedirectUri)))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, InvalidRequestMessage);

        var issuer = IssuerResolver.Resolve(httpContext.Request);

        if (!decision.Approved)
            return DecisionRedirect(OAuthRedirectUriComposer.AppendParameters(
                authorizationRequest.RedirectUri,
                ("error", OAuthErrorCodes.AccessDenied),
                ("error_description", "The user denied the authorization request."),
                ("state", authorizationRequest.State),
                ("iss", issuer)));

        if (!await GrantService.IsUserActiveAsync(userId, httpContext.RequestAborted))
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var code = await GrantService.CreateAuthorizationAsync(authorizationRequest, userId, httpContext.RequestAborted);

        return DecisionRedirect(OAuthRedirectUriComposer.AppendParameters(
            authorizationRequest.RedirectUri,
            ("code", code),
            ("state", authorizationRequest.State),
            ("iss", issuer)));
    }

    /// <summary>
    /// The account allowed to decide: signed in through the application's own session (never through
    /// an access token, which must not be able to authorize further clients), and holding one of
    /// <see cref="OAuthServerOptions.RequiredRoles"/> when any are configured.
    /// </summary>
    private string? ResolveApprovingUserId(ClaimsPrincipal user)
    {
        if (user.HasClaim(claim => claim.Type == AccessTokenClaimTypes.TokenId))
            return null;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
            return null;

        var requiredRoles = Options.CurrentValue.RequiredRoles;

        if (requiredRoles.Count > 0 && !requiredRoles.Any(user.IsInRole))
            return null;

        return userId;
    }

    private bool IsRegisteredResource(string resource, string issuer) =>
        ResourceRegistry.ResourcePaths.Any(resourcePath => OAuthResourceIdentifier.AreEquivalent(issuer + resourcePath, resource));

    private static IResult Reject(string message) => Results.Text(message, "text/plain", statusCode: StatusCodes.Status400BadRequest);

    private static IResult RedirectWithError(string redirectUri, string error, string description, string? state, string issuer) =>
        Results.Redirect(OAuthRedirectUriComposer.AppendParameters(redirectUri, ("error", error), ("error_description", description), ("state", state), ("iss", issuer)));

    private static IResult DecisionRedirect(string redirectUri) =>
        Results.Ok(new OAuthAuthorizationDecisionResponse { RedirectUri = redirectUri });
}
