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

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            await AuthStateProvider.InvalidateAsync();

        return response;
    }
}
