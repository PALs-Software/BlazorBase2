using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// Registers and looks up OAuth clients. The <c>register</c> endpoint calls
/// <see cref="RegisterAsync"/> for dynamic client registration; a host that turns
/// <c>EnableDynamicClientRegistration</c> off calls it itself to pre-register clients, with the same
/// validation.
/// </summary>
public interface IOAuthClientService
{
    /// <summary>
    /// Validates <paramref name="request"/> as RFC 7591 client metadata and stores the client. A
    /// confidential client's secret is in the returned response exactly once; only its hash is stored.
    /// </summary>
    Task<OAuthClientRegistrationResult> RegisterAsync(OAuthClientRegistrationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the client with <paramref name="clientId"/>, or why there is none. An <c>https</c> client
    /// id is resolved through its Client ID Metadata Document, fetched or taken from the cache.
    /// </summary>
    Task<OAuthClientResolution> ResolveAsync(string clientId, CancellationToken cancellationToken = default);

    /// <summary>Returns the client with <paramref name="clientId"/> as <see cref="ResolveAsync"/> does, or <see langword="null"/>.</summary>
    Task<OAuthClient?> FindAsync(string clientId, CancellationToken cancellationToken = default);
}
