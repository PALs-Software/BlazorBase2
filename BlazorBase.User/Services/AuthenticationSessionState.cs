using System.Security.Claims;

namespace BlazorBase.User.Services;

/// <summary>
/// The principal last read from <c>GET /api/auth/session</c>, shared by every
/// <see cref="BlazorBaseUserAuthStateProvider"/> that resolves this instance, plus the notification
/// that tells all of them when it changes.
/// </summary>
/// <remarks>
/// IHttpClientFactory builds every <see cref="DelegatingHandler"/> in a dependency injection scope of
/// its own. A provider resolved by <see cref="AuthTokenHandler"/> or <see cref="SessionExpiryHandler"/>
/// is therefore a different instance from the one the components render with, and without shared state
/// a 401 or a failed refresh dropped only the handler's copy to anonymous while the UI kept showing the
/// signed-in user. In a single-user process (WebAssembly, MAUI) this is registered once per process by
/// <see cref="BlazorBaseUserServiceCollectionExtensions.AddBlazorBaseUserSharedClientSession"/>.
/// </remarks>
public sealed class AuthenticationSessionState
{
    public ClaimsPrincipal? Principal { get; private set; }

    /// <summary>
    /// Raised by <see cref="Announce"/>, never by <see cref="Remember"/>.
    /// </summary>
    public event Action<ClaimsPrincipal>? Changed;

    /// <summary>
    /// Caches the principal read on demand. Nobody needs to hear about it: every other provider would
    /// have read the same answer.
    /// </summary>
    public void Remember(ClaimsPrincipal principal) => Principal = principal;

    /// <summary>
    /// Drops the cache so the next read asks the server again.
    /// </summary>
    public void Forget() => Principal = null;

    /// <summary>
    /// Caches the principal and tells every provider sharing this state, after a sign-in, a refresh or
    /// a sign-out.
    /// </summary>
    public void Announce(ClaimsPrincipal principal)
    {
        Principal = principal;
        Changed?.Invoke(principal);
    }
}
