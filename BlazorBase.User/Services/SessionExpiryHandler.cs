using System.Net;

namespace BlazorBase.User.Services;

/// <summary>
/// Drops the cached authentication state to anonymous whenever the API answers 401.
/// </summary>
/// <remarks>
/// Without this, a session that has expired or been revoked server-side leaves
/// <see cref="BlazorBaseUserAuthStateProvider"/> reporting its last cached "signed in" principal while
/// every subsequent request is refused — the UI shows a broken app instead of a login screen. A host
/// registers this on the typed client(s) that call the API, the same way it registers
/// <see cref="AuthTokenHandler"/> or <c>CsrfHeaderHandler</c>.
///
/// Under the bearer transport this handler defers to <see cref="AuthTokenHandler"/> for a request that
/// handler can recover: a 401 on a request sent while a refresh token is held is answered by the refresh
/// and retry, and a refresh the server rejects signs the user out there. This handler still drops the state
/// for a 401 that <see cref="AuthTokenHandler"/> cannot recover, such as a request without a refresh token
/// or one to an auth endpoint. The two may be chained in either order, because the decision travels on the
/// request (<see cref="UnauthorizedRecoveryRequestOptions.HandledByAuthTokenHandler"/>), not on the chain
/// position.
/// </remarks>
public sealed class SessionExpiryHandler(BlazorBaseUserAuthStateProvider authStateProvider) : DelegatingHandler
{
    #region Injects
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;
    #endregion

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (request.Options.TryGetValue(UnauthorizedRecoveryRequestOptions.HandledByAuthTokenHandler, out var handledByAuthTokenHandler)
            && handledByAuthTokenHandler)
            return response;

        await AuthStateProvider.InvalidateAsync();

        return response;
    }
}
