using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Endpoints;
using BlazorBase.User.Server.OAuth.Grants;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Tokens;

/// <summary>
/// The back channel: redeems authorization codes and refresh tokens for an access token, and revokes
/// tokens on the client's request. The access token is an ordinary <see cref="AccessToken"/> row
/// carrying <see cref="AccessToken.OAuthGrantId"/>, so the existing access-token scheme authenticates
/// it, its owner's roles apply through the usual claims augmentors, and revocation is immediate.
/// </summary>
/// <remarks>
/// Codes and refresh tokens are consumed by a conditional update (<c>WHERE ConsumedAt IS NULL</c>), so
/// two parallel redemptions of the same value cannot both succeed. A redeemed code presented again
/// revokes its grant (RFC 6749 section 4.1.2); a redeemed refresh token presented again after
/// <see cref="OAuthServerOptions.RefreshTokenReuseGracePeriod"/> does too (OAuth 2.1 refresh token
/// rotation with reuse detection).
/// </remarks>
internal sealed class OAuthTokenEndpointHandler<TUser>(
    BaseUserDbContext<TUser> dbContext,
    OAuthClientAuthenticator clientAuthenticator,
    OAuthGrantService<TUser> grantService,
    IAccessTokenService accessTokenService,
    IOptionsMonitor<OAuthServerOptions> options,
    TimeProvider timeProvider,
    ILogger<OAuthTokenEndpointHandler<TUser>> logger) : IOAuthTokenEndpointHandler
    where TUser : BaseUser
{
    #region Injects
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly OAuthClientAuthenticator ClientAuthenticator = clientAuthenticator;
    private readonly OAuthGrantService<TUser> GrantService = grantService;
    private readonly IAccessTokenService AccessTokenService = accessTokenService;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ILogger<OAuthTokenEndpointHandler<TUser>> Logger = logger;
    #endregion

    private const int MaximumAccessTokenNameLength = 100;
    private const string AccessTokenNamePrefix = "OAuth: ";
    private const string InvalidGrantDescription = "The grant is invalid, expired, revoked or was issued to another client.";

    public async Task<IResult> TokenAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.HasFormContentType)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "The token request must be application/x-www-form-urlencoded.");

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        var authentication = await ClientAuthenticator.AuthenticateAsync(httpContext.Request, form, httpContext.RequestAborted);

        if (authentication.Client is null)
            return OAuthErrorResults.Error(httpContext, authentication.Error!, authentication.UsedBasicAuthentication);

        if (!OAuthParameterReader.TryReadSingle(form["grant_type"], out var grantType))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "grant_type must not be repeated.");

        return grantType switch
        {
            OAuthGrantTypes.AuthorizationCode => await RedeemAuthorizationCodeAsync(httpContext, form, authentication.Client),
            OAuthGrantTypes.RefreshToken => await RedeemRefreshTokenAsync(httpContext, form, authentication.Client),
            null => OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "grant_type is required."),
            _ => OAuthErrorResults.Error(httpContext, OAuthErrorCodes.UnsupportedGrantType, "Only authorization_code and refresh_token are supported."),
        };
    }

    public async Task<IResult> RevokeAsync(HttpContext httpContext)
    {
        if (!httpContext.Request.HasFormContentType)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "The revocation request must be application/x-www-form-urlencoded.");

        var form = await httpContext.Request.ReadFormAsync(httpContext.RequestAborted);
        var authentication = await ClientAuthenticator.AuthenticateAsync(httpContext.Request, form, httpContext.RequestAborted);

        if (authentication.Client is null)
            return OAuthErrorResults.Error(httpContext, authentication.Error!, authentication.UsedBasicAuthentication);

        if (!OAuthParameterReader.TryReadSingle(form["token"], out var token) || token is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "token is required and must not be repeated.");

        if (!await TryRevokeRefreshTokenAsync(token, authentication.Client, httpContext.RequestAborted))
            await TryRevokeAccessTokenAsync(token, authentication.Client, httpContext.RequestAborted);

        return Results.Ok();
    }

    private async Task<IResult> RedeemAuthorizationCodeAsync(HttpContext httpContext, IFormCollection form, OAuthClient client)
    {
        var parametersReadable = OAuthParameterReader.TryReadSingle(form["code"], out var code)
            & OAuthParameterReader.TryReadSingle(form["redirect_uri"], out var redirectUri)
            & OAuthParameterReader.TryReadSingle(form["code_verifier"], out var codeVerifier)
            & OAuthParameterReader.TryReadSingle(form["resource"], out var resource);

        if (!parametersReadable)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "A request parameter was repeated.");

        if (code is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "code is required.");

        if (codeVerifier is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "code_verifier is required (PKCE).");

        var cancellationToken = httpContext.RequestAborted;
        var codeHash = OAuthSecretGenerator.Hash(code);
        var storedCode = await DbContext.OAuthAuthorizationCodes.AsNoTracking().FirstOrDefaultAsync(entry => entry.CodeHash == codeHash, cancellationToken);

        if (storedCode is null)
            return InvalidGrant(httpContext);

        var grant = await DbContext.OAuthGrants.FirstOrDefaultAsync(entry => entry.Id == storedCode.GrantId, cancellationToken);

        if (grant is null || grant.RevokedAt is not null || grant.ClientId != client.Id)
            return InvalidGrant(httpContext);

        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        if (storedCode.ConsumedAt is not null)
            return await RevokeAfterReplayAsync(httpContext, grant, "authorization code");

        if (storedCode.ExpiresAt < utcNow)
            return InvalidGrant(httpContext);

        if (!RedirectUriMatches(storedCode, grant, redirectUri))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidGrant, "redirect_uri does not match the authorization request.");

        if (!OAuthPkce.IsValidVerifier(codeVerifier) || !OAuthPkce.Matches(codeVerifier, storedCode.CodeChallenge))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidGrant, "code_verifier does not match the code_challenge.");

        if (resource is not null && !OAuthResourceIdentifier.AreEquivalent(resource, grant.Resource))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidTarget, "resource does not match the authorization request.");

        if (!await GrantService.IsUserEntitledAsync(grant.UserId, cancellationToken))
            return await RevokeAfterLostEntitlementAsync(httpContext, grant);

        var consumed = await DbContext.OAuthAuthorizationCodes
            .Where(entry => entry.Id == storedCode.Id && entry.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.ConsumedAt, utcNow), cancellationToken);

        if (consumed == 0)
            return await RevokeAfterReplayAsync(httpContext, grant, "authorization code");

        return await TokenResponseAsync(httpContext, grant, client, utcNow);
    }

    private async Task<IResult> RedeemRefreshTokenAsync(HttpContext httpContext, IFormCollection form, OAuthClient client)
    {
        var parametersReadable = OAuthParameterReader.TryReadSingle(form["refresh_token"], out var refreshToken)
            & OAuthParameterReader.TryReadSingle(form["scope"], out var scope)
            & OAuthParameterReader.TryReadSingle(form["resource"], out var resource);

        if (!parametersReadable)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "A request parameter was repeated.");

        if (refreshToken is null)
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidRequest, "refresh_token is required.");

        var cancellationToken = httpContext.RequestAborted;
        var tokenHash = OAuthSecretGenerator.Hash(refreshToken);
        var storedToken = await DbContext.OAuthRefreshTokens.AsNoTracking().FirstOrDefaultAsync(entry => entry.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
            return InvalidGrant(httpContext);

        var grant = await DbContext.OAuthGrants.FirstOrDefaultAsync(entry => entry.Id == storedToken.GrantId, cancellationToken);

        if (grant is null || grant.RevokedAt is not null || grant.ClientId != client.Id)
            return InvalidGrant(httpContext);

        if (!client.GrantTypes.Contains(OAuthGrantTypes.RefreshToken))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.UnauthorizedClient, "The client is not registered for the refresh_token grant.");

        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;

        if (storedToken.ConsumedAt is { } consumedAt)
        {
            if (utcNow - consumedAt <= Options.CurrentValue.RefreshTokenReuseGracePeriod)
                return InvalidGrant(httpContext);

            return await RevokeAfterReplayAsync(httpContext, grant, "refresh token");
        }

        if (storedToken.ExpiresAt < utcNow)
            return InvalidGrant(httpContext);

        var grantedScopes = OAuthScopes.Parse(grant.Scope);

        if (OAuthScopes.Parse(scope).Any(requestedScope => !grantedScopes.Contains(requestedScope)))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidScope, "scope exceeds the scope of the grant.");

        if (resource is not null && !OAuthResourceIdentifier.AreEquivalent(resource, grant.Resource))
            return OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidTarget, "resource does not match the grant.");

        if (!await GrantService.IsUserEntitledAsync(grant.UserId, cancellationToken))
            return await RevokeAfterLostEntitlementAsync(httpContext, grant);

        var consumed = await DbContext.OAuthRefreshTokens
            .Where(entry => entry.Id == storedToken.Id && entry.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(entry => entry.ConsumedAt, utcNow), cancellationToken);

        if (consumed == 0)
            return InvalidGrant(httpContext);

        return await TokenResponseAsync(httpContext, grant, client, utcNow);
    }

    /// <summary>
    /// Issues the access token through <see cref="IAccessTokenService"/> and, for a client registered for
    /// the <c>refresh_token</c> grant, a new refresh token. The refresh token row and the
    /// <c>LastUsedAt</c> updates are pending on the shared context when the access token is created, so
    /// the one save that persists the access token persists them too. Returns <see langword="null"/>
    /// when the grant was revoked while the tokens were being issued; the new tokens are revoked then.
    /// </summary>
    /// <remarks>
    /// Revocation marks the grant before it revokes the grant's tokens, and issuance saves the tokens
    /// before it re-reads the grant. Whatever the interleaving, either the revocation's bulk update sees
    /// the new tokens or this re-read sees the revoked grant, so no token outlives a concurrent revocation.
    /// </remarks>
    private async Task<OAuthTokenResponse?> IssueTokensAsync(OAuthGrant grant, OAuthClient client, DateTime utcNow, CancellationToken cancellationToken)
    {
        var serverOptions = Options.CurrentValue;
        string? refreshToken = null;

        grant.LastUsedAt = utcNow;
        client.LastUsedAt = utcNow;

        if (client.GrantTypes.Contains(OAuthGrantTypes.RefreshToken))
        {
            refreshToken = OAuthSecretGenerator.CreateSecret();

            DbContext.OAuthRefreshTokens.Add(new OAuthRefreshToken
            {
                Id = Guid.NewGuid(),
                TokenHash = OAuthSecretGenerator.Hash(refreshToken),
                GrantId = grant.Id,
                CreatedAt = utcNow,
                ExpiresAt = utcNow + serverOptions.RefreshTokenLifetime,
            });
        }

        var accessToken = await AccessTokenService.CreateAsync<AccessToken>(
            BuildAccessTokenName(client),
            utcNow + serverOptions.AccessTokenLifetime,
            token => token.OAuthGrantId = grant.Id,
            grant.UserId,
            cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        if (await GrantWasRevokedMeanwhileAsync(grant.Id, cancellationToken))
        {
            await GrantService.RevokeGrantAsync(grant.Id, cancellationToken);
            return null;
        }

        return new OAuthTokenResponse
        {
            AccessToken = accessToken.Secret,
            ExpiresIn = (long)serverOptions.AccessTokenLifetime.TotalSeconds,
            RefreshToken = refreshToken,
            Scope = grant.Scope,
        };
    }

    private async Task<bool> TryRevokeRefreshTokenAsync(string token, OAuthClient client, CancellationToken cancellationToken)
    {
        var tokenHash = OAuthSecretGenerator.Hash(token);
        var grantId = await DbContext.OAuthRefreshTokens
            .Where(entry => entry.TokenHash == tokenHash)
            .Select(entry => (Guid?)entry.GrantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (grantId is null)
            return false;

        if (await GrantBelongsToClientAsync(grantId.Value, client, cancellationToken))
            await GrantService.RevokeGrantAsync(grantId.Value, cancellationToken);

        return true;
    }

    private async Task TryRevokeAccessTokenAsync(string token, OAuthClient client, CancellationToken cancellationToken)
    {
        var validated = await AccessTokenService.ValidateAsync(token, cancellationToken);

        if (validated?.Entity.OAuthGrantId is not { } grantId)
            return;

        if (await GrantBelongsToClientAsync(grantId, client, cancellationToken))
            await AccessTokenService.RevokeAsync(validated.TokenId, cancellationToken: cancellationToken);
    }

    private async Task<bool> GrantWasRevokedMeanwhileAsync(Guid grantId, CancellationToken cancellationToken) =>
        await DbContext.OAuthGrants.AsNoTracking().AnyAsync(grant => grant.Id == grantId && grant.RevokedAt != null, cancellationToken);

    private async Task<IResult> TokenResponseAsync(HttpContext httpContext, OAuthGrant grant, OAuthClient client, DateTime utcNow)
    {
        var response = await IssueTokensAsync(grant, client, utcNow, httpContext.RequestAborted);

        if (response is null)
            return InvalidGrant(httpContext);

        return Results.Json(response);
    }

    private async Task<bool> GrantBelongsToClientAsync(Guid grantId, OAuthClient client, CancellationToken cancellationToken) =>
        await DbContext.OAuthGrants.AnyAsync(grant => grant.Id == grantId && grant.ClientId == client.Id, cancellationToken);

    private async Task<IResult> RevokeAfterReplayAsync(HttpContext httpContext, OAuthGrant grant, string credentialKind)
    {
        await GrantService.RevokeGrantAsync(grant.Id, httpContext.RequestAborted);
        Logger.LogWarning("A redeemed OAuth {CredentialKind} was presented again for grant {GrantId} of client {ClientId}; the grant has been revoked.", credentialKind, grant.Id, grant.ClientId);

        return InvalidGrant(httpContext);
    }

    /// <summary>
    /// The grant's user was deactivated, deleted or lost every <see cref="OAuthServerOptions.RequiredRoles"/> role
    /// since the consent: the grant is revoked, so the access tokens already issued under it stop at once too.
    /// </summary>
    private async Task<IResult> RevokeAfterLostEntitlementAsync(HttpContext httpContext, OAuthGrant grant)
    {
        await GrantService.RevokeGrantAsync(grant.Id, httpContext.RequestAborted);
        Logger.LogInformation("The user of OAuth grant {GrantId} of client {ClientId} is no longer entitled to it; the grant has been revoked.", grant.Id, grant.ClientId);

        return InvalidGrant(httpContext);
    }

    /// <summary>
    /// Matches RFC 6749 section 4.1.3: a token request must repeat the <c>redirect_uri</c> the
    /// authorization request named; when that request named none, a supplied value must still be the
    /// URI the authorization was answered on.
    /// </summary>
    private static bool RedirectUriMatches(OAuthAuthorizationCode storedCode, OAuthGrant grant, string? redirectUri)
    {
        if (storedCode.RedirectUri is not null)
            return string.Equals(redirectUri, storedCode.RedirectUri, StringComparison.Ordinal);

        return redirectUri is null || string.Equals(redirectUri, grant.RedirectUri, StringComparison.Ordinal);
    }

    private static string BuildAccessTokenName(OAuthClient client)
    {
        var name = AccessTokenNamePrefix + client.ClientName;
        return name.Length <= MaximumAccessTokenNameLength ? name : name[..MaximumAccessTokenNameLength];
    }

    private static IResult InvalidGrant(HttpContext httpContext) =>
        OAuthErrorResults.Error(httpContext, OAuthErrorCodes.InvalidGrant, InvalidGrantDescription);
}
