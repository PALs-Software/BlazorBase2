using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.ClientMetadataDocuments;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The <see cref="IOAuthClientService"/> over <see cref="BaseUserDbContext{TUser}"/>.
/// </summary>
public sealed class OAuthClientService<TUser>(
    BaseUserDbContext<TUser> dbContext,
    OAuthClientIdMetadataDocumentResolver<TUser> metadataDocumentResolver,
    IOptionsMonitor<OAuthServerOptions> options,
    TimeProvider timeProvider) : IOAuthClientService
    where TUser : BaseUser
{
    #region Injects
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly OAuthClientIdMetadataDocumentResolver<TUser> MetadataDocumentResolver = metadataDocumentResolver;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    #endregion

    private const string UnknownClientMessage = "The client is unknown.";
    private const string DefaultClientName = "Unnamed client";
    private const int MaximumClientNameLength = 200;
    private const string CodeResponseType = "code";

    /// <inheritdoc/>
    public async Task<OAuthClientRegistrationResult> RegisterAsync(OAuthClientRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var redirectUris = request.RedirectUris ?? [];

        if (redirectUris.Count == 0)
            return OAuthClientRegistrationResult.Failure(OAuthErrorCodes.InvalidRedirectUri, "redirect_uris must contain at least one redirect URI.");

        if (redirectUris.Count > OAuthRedirectUriValidator.MaximumRedirectUriCount)
            return OAuthClientRegistrationResult.Failure(OAuthErrorCodes.InvalidRedirectUri, $"At most {OAuthRedirectUriValidator.MaximumRedirectUriCount} redirect URIs may be registered.");

        var serverOptions = Options.CurrentValue;

        foreach (var redirectUri in redirectUris)
        {
            var violation = OAuthRedirectUriValidator.Validate(redirectUri, serverOptions);

            if (violation is not null)
                return OAuthClientRegistrationResult.Failure(OAuthErrorCodes.InvalidRedirectUri, violation);
        }

        var metadataError = ValidateMetadata(request);

        if (metadataError is not null)
            return OAuthClientRegistrationResult.Failure(OAuthErrorCodes.InvalidClientMetadata, metadataError);

        var authenticationMethod = request.TokenEndpointAuthMethod ?? OAuthClientAuthenticationMethods.ClientSecretBasic;
        var grantTypes = request.GrantTypes is { Count: > 0 } ? request.GrantTypes.Distinct(StringComparer.Ordinal).ToList() : [OAuthGrantTypes.AuthorizationCode];
        var clientSecret = OAuthClientAuthenticationMethods.UsesSecret(authenticationMethod) ? OAuthSecretGenerator.CreateSecret() : null;
        var issuedAt = TimeProvider.GetUtcNow();

        var client = new OAuthClient
        {
            Id = OAuthSecretGenerator.CreateClientId(),
            ClientName = string.IsNullOrWhiteSpace(request.ClientName) ? DefaultClientName : request.ClientName.Trim(),
            RedirectUris = redirectUris.Distinct(StringComparer.Ordinal).ToList(),
            GrantTypes = grantTypes,
            TokenEndpointAuthMethod = authenticationMethod,
            ClientSecretHash = clientSecret is null ? null : OAuthSecretGenerator.Hash(clientSecret),
            CreatedAt = issuedAt.UtcDateTime,
        };

        DbContext.OAuthClients.Add(client);
        await DbContext.SaveChangesAsync(cancellationToken);

        return OAuthClientRegistrationResult.Success(new OAuthClientRegistrationResponse
        {
            ClientId = client.Id,
            ClientIdIssuedAt = issuedAt.ToUnixTimeSeconds(),
            ClientName = client.ClientName,
            RedirectUris = client.RedirectUris,
            GrantTypes = client.GrantTypes,
            ResponseTypes = [CodeResponseType],
            TokenEndpointAuthMethod = client.TokenEndpointAuthMethod,
            ClientSecret = clientSecret,
            ClientSecretExpiresAt = clientSecret is null ? null : 0,
        });
    }

    /// <inheritdoc/>
    public async Task<OAuthClientResolution> ResolveAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(clientId))
            return OAuthClientResolution.NotFound(UnknownClientMessage);

        if (OAuthClientIdMetadataDocumentUrl.IsCandidate(clientId))
            return await MetadataDocumentResolver.ResolveAsync(clientId, cancellationToken);

        var client = await DbContext.OAuthClients
            .FirstOrDefaultAsync(storedClient => storedClient.Id == clientId && !storedClient.IsMetadataDocumentClient, cancellationToken);

        if (client is null || !string.Equals(client.Id, clientId, StringComparison.Ordinal))
            return OAuthClientResolution.NotFound(UnknownClientMessage);

        return OAuthClientResolution.Found(client);
    }

    /// <inheritdoc/>
    public async Task<OAuthClient?> FindAsync(string clientId, CancellationToken cancellationToken = default) =>
        (await ResolveAsync(clientId, cancellationToken)).Client;

    private static string? ValidateMetadata(OAuthClientRegistrationRequest request)
    {
        if (request.ClientName is not null && request.ClientName.Trim().Length > MaximumClientNameLength)
            return $"client_name must not exceed {MaximumClientNameLength} characters.";

        if (request.TokenEndpointAuthMethod is not null && !OAuthClientAuthenticationMethods.All.Contains(request.TokenEndpointAuthMethod))
            return $"token_endpoint_auth_method '{request.TokenEndpointAuthMethod}' is not supported; use none, client_secret_post or client_secret_basic.";

        if (request.GrantTypes is { Count: > 0 } grantTypes)
        {
            var unsupportedGrantType = grantTypes.FirstOrDefault(grantType => !OAuthGrantTypes.All.Contains(grantType));

            if (unsupportedGrantType is not null)
                return $"grant_type '{unsupportedGrantType}' is not supported; use authorization_code and refresh_token.";

            if (!grantTypes.Contains(OAuthGrantTypes.AuthorizationCode))
                return "grant_types must include authorization_code, the only grant that starts an authorization.";
        }

        if (request.ResponseTypes is { Count: > 0 } responseTypes && responseTypes.Any(responseType => responseType != CodeResponseType))
            return "response_types may only contain code.";

        return null;
    }
}
