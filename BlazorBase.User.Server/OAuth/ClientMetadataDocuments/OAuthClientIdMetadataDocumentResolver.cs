using BlazorBase.User.Server.Data;
using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Grants;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// Turns an <c>https</c> client id into a client by fetching its Client ID Metadata Document, and keeps
/// the result as an <see cref="OAuthClient"/> row so grants can reference it and the document is not
/// fetched on every request.
/// </summary>
/// <remarks>
/// A cached client is used until <see cref="OAuthClient.MetadataDocumentExpiresAt"/>: the document's
/// <c>Cache-Control</c> lifetime, clamped to the configured minimum and maximum. When the document cannot
/// be fetched (network, timeout, server error) a cached client stays usable - for at most twice the maximum
/// cache duration after its last successful fetch - and the next attempt waits for the minimum duration.
/// New clients are refused while <see cref="OAuthServerOptions.MaximumUnusedClientIdMetadataDocumentClients"/>
/// clients without a grant exist. When a fetched document is refused - its <c>client_id</c> no longer matches,
/// or it now breaks a rule - every grant of the cached client is revoked, as the draft recommends for a
/// document that can no longer be trusted (section 6.1).
/// <para>
/// The database may compare client ids case-insensitively (the SQL Server default collation), while a URL
/// path is case-sensitive. A client id that finds a stored row without being ordinally equal to its id is
/// therefore refused before anything is fetched, so a case variant can neither borrow a cached client nor
/// trigger the revocation of the real client's grants.
/// </para>
/// </remarks>
public sealed class OAuthClientIdMetadataDocumentResolver<TUser>(
    BaseUserDbContext<TUser> dbContext,
    IOAuthClientIdMetadataDocumentFetcher fetcher,
    OAuthGrantService<TUser> grantService,
    IOptionsMonitor<OAuthServerOptions> options,
    TimeProvider timeProvider,
    ILogger<OAuthClientIdMetadataDocumentResolver<TUser>> logger)
    where TUser : BaseUser
{
    #region Injects
    private readonly BaseUserDbContext<TUser> DbContext = dbContext;
    private readonly IOAuthClientIdMetadataDocumentFetcher Fetcher = fetcher;
    private readonly OAuthGrantService<TUser> GrantService = grantService;
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly TimeProvider TimeProvider = timeProvider;
    private readonly ILogger<OAuthClientIdMetadataDocumentResolver<TUser>> Logger = logger;
    #endregion

    /// <summary>
    /// A cached client stays usable through fetch failures for this many maximum cache durations after
    /// its last successful fetch - long enough to ride out an outage, never indefinitely.
    /// </summary>
    private const int StaleUseFactor = 2;

    private const string TooManyUnusedClientsMessage = "Too many client id metadata documents are waiting for a first authorization; try again later.";

    /// <summary>Resolves <paramref name="clientIdUrl"/>, an id for which <see cref="OAuthClientIdMetadataDocumentUrl.IsCandidate"/> holds.</summary>
    public async Task<OAuthClientResolution> ResolveAsync(string clientIdUrl, CancellationToken cancellationToken = default)
    {
        var serverOptions = Options.CurrentValue;

        if (!serverOptions.EnableClientIdMetadataDocuments)
            return OAuthClientResolution.NotFound("This server does not accept client id metadata documents.");

        var urlViolation = OAuthClientIdMetadataDocumentUrl.Validate(clientIdUrl, serverOptions);

        if (urlViolation is not null)
            return OAuthClientResolution.NotFound(urlViolation);

        var utcNow = TimeProvider.GetUtcNow().UtcDateTime;
        var cachedClient = await DbContext.OAuthClients.FirstOrDefaultAsync(client => client.Id == clientIdUrl, cancellationToken);

        if (cachedClient is not null && (!cachedClient.IsMetadataDocumentClient || !string.Equals(cachedClient.Id, clientIdUrl, StringComparison.Ordinal)))
            return OAuthClientResolution.NotFound("The client id differs from a known client id only in letter case.");

        if (cachedClient is not null && cachedClient.MetadataDocumentExpiresAt > utcNow)
            return OAuthClientResolution.Found(cachedClient);

        var fetchResult = await Fetcher.FetchAsync(clientIdUrl, cancellationToken);

        if (fetchResult.Status == OAuthClientIdMetadataDocumentFetchStatus.TransientFailure)
            return await KeepCachedClientAsync(clientIdUrl, cachedClient, fetchResult.Error!, utcNow, cancellationToken);

        if (fetchResult.Document is null)
        {
            Logger.LogWarning("The client id metadata document at {ClientId} was refused: {Error}", clientIdUrl, fetchResult.Error);
            return OAuthClientResolution.NotFound(fetchResult.Error!);
        }

        var validation = OAuthClientIdMetadataDocumentValidator.Validate(clientIdUrl, fetchResult.Document, serverOptions);

        if (!validation.IsValid)
            return await RefuseDocumentAsync(clientIdUrl, cachedClient, validation.Violation!, cancellationToken);

        return await StoreClientAsync(clientIdUrl, cachedClient, validation, fetchResult.CacheDuration, utcNow, cancellationToken);
    }

    private async Task<OAuthClientResolution> KeepCachedClientAsync(string clientIdUrl, OAuthClient? cachedClient, string error, DateTime utcNow, CancellationToken cancellationToken)
    {
        Logger.LogWarning("The client id metadata document at {ClientId} could not be fetched: {Error}", clientIdUrl, error);

        if (cachedClient is null)
            return OAuthClientResolution.NotFound(error);

        var serverOptions = Options.CurrentValue;
        var staleUseEndsAt = cachedClient.MetadataDocumentFetchedAt + StaleUseFactor * serverOptions.ClientIdMetadataDocumentMaximumCacheDuration;

        if (staleUseEndsAt is null || utcNow >= staleUseEndsAt)
            return OAuthClientResolution.NotFound(error);

        cachedClient.MetadataDocumentExpiresAt = utcNow + serverOptions.ClientIdMetadataDocumentMinimumCacheDuration;
        await DbContext.SaveChangesAsync(cancellationToken);

        return OAuthClientResolution.Found(cachedClient);
    }

    private async Task<OAuthClientResolution> RefuseDocumentAsync(string clientIdUrl, OAuthClient? cachedClient, string violation, CancellationToken cancellationToken)
    {
        Logger.LogWarning("The client id metadata document at {ClientId} was refused: {Violation}", clientIdUrl, violation);

        if (cachedClient is not null)
        {
            var activeGrantIds = await DbContext.OAuthGrants
                .Where(grant => grant.ClientId == cachedClient.Id && grant.RevokedAt == null)
                .Select(grant => grant.Id)
                .ToListAsync(cancellationToken);

            foreach (var grantId in activeGrantIds)
                await GrantService.RevokeGrantAsync(grantId, cancellationToken);
        }

        return OAuthClientResolution.NotFound(violation);
    }

    private async Task<OAuthClientResolution> StoreClientAsync(
        string clientIdUrl,
        OAuthClient? cachedClient,
        OAuthClientIdMetadataDocumentValidationResult validation,
        TimeSpan? cacheDuration,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (cachedClient is null && await HasTooManyUnusedClientsAsync(cancellationToken))
        {
            Logger.LogWarning("Refused the client id metadata document at {ClientId}: the limit of unused metadata document clients is reached.", clientIdUrl);
            return OAuthClientResolution.NotFound(TooManyUnusedClientsMessage);
        }

        var client = cachedClient ?? new OAuthClient
        {
            Id = clientIdUrl,
            IsMetadataDocumentClient = true,
            TokenEndpointAuthMethod = OAuthClientAuthenticationMethods.None,
            CreatedAt = utcNow,
        };

        client.ClientName = validation.ClientName;
        client.RedirectUris = validation.RedirectUris.ToList();
        client.GrantTypes = validation.GrantTypes.ToList();
        client.MetadataDocumentExpiresAt = utcNow + EffectiveCacheDuration(cacheDuration);
        client.MetadataDocumentFetchedAt = utcNow;

        if (cachedClient is null)
            DbContext.OAuthClients.Add(client);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (cachedClient is null)
        {
            DbContext.Entry(client).State = EntityState.Detached;
            var concurrentlyStoredClient = await DbContext.OAuthClients.FirstOrDefaultAsync(storedClient => storedClient.Id == clientIdUrl, cancellationToken);

            if (concurrentlyStoredClient is null)
                throw;

            if (!concurrentlyStoredClient.IsMetadataDocumentClient || !string.Equals(concurrentlyStoredClient.Id, clientIdUrl, StringComparison.Ordinal))
                return OAuthClientResolution.NotFound("The client id differs from a known client id only in letter case.");

            return OAuthClientResolution.Found(concurrentlyStoredClient);
        }

        return OAuthClientResolution.Found(client);
    }

    private async Task<bool> HasTooManyUnusedClientsAsync(CancellationToken cancellationToken)
    {
        var unusedClientCount = await DbContext.OAuthClients
            .CountAsync(client => client.IsMetadataDocumentClient && !DbContext.OAuthGrants.Any(grant => grant.ClientId == client.Id), cancellationToken);

        return unusedClientCount >= Options.CurrentValue.MaximumUnusedClientIdMetadataDocumentClients;
    }

    private TimeSpan EffectiveCacheDuration(TimeSpan? cacheDuration)
    {
        var serverOptions = Options.CurrentValue;
        var requestedDuration = cacheDuration ?? serverOptions.ClientIdMetadataDocumentMinimumCacheDuration;

        if (requestedDuration < serverOptions.ClientIdMetadataDocumentMinimumCacheDuration)
            return serverOptions.ClientIdMetadataDocumentMinimumCacheDuration;

        if (requestedDuration > serverOptions.ClientIdMetadataDocumentMaximumCacheDuration)
            return serverOptions.ClientIdMetadataDocumentMaximumCacheDuration;

        return requestedDuration;
    }
}
