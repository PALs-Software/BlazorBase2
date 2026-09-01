using System.Security.Claims;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Decides how an issued token pair reaches the client, and where it is read back from.
/// </summary>
/// <remarks>
/// Two shapes of client cannot share one answer. A browser is best served by ASP.NET Core Identity's
/// own application cookie, which it cannot read and which the platform revokes and refreshes for us.
/// A native or service-to-service client has no cookie jar worth the name and needs the token in hand.
/// The token pair itself is identical either way — only its delivery differs, so this seam sits after
/// <c>TokenService.IssueTokenPairAsync</c> rather than inside it.
/// </remarks>
public interface IAuthTokenTransport
{
    /// <summary>
    /// Delivers <paramref name="tokens"/> and returns the payload the endpoint should serialise.
    /// </summary>
    /// <remarks>
    /// An implementation that puts the tokens somewhere the client cannot read must blank them in
    /// the returned value. Sending them in the body as well would hand them straight back to the
    /// script the transport exists to keep them from.
    ///
    /// <paramref name="principal"/> is the caller's application principal, built the same way the
    /// caller was authenticated — a cookie-based implementation needs it to sign in with
    /// <see cref="Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignInAsync(HttpContext, string, ClaimsPrincipal)"/>.
    /// It is a <see cref="ClaimsPrincipal"/> rather than the concrete user type on purpose: this
    /// interface, and every service that registers a default implementation of it, is resolved
    /// without a <c>TUser</c> type parameter in scope, so a signature that needed one would not
    /// compile at those call sites.
    /// </remarks>
    Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, ClaimsPrincipal principal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the refresh token for a rotation request. <paramref name="fromRequestBody"/> is
    /// whatever the caller sent, which a cookie-based implementation is free to ignore.
    /// </summary>
    Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default);

    /// <summary>Removes anything <see cref="WriteAsync"/> left on the client. Called on logout.</summary>
    Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a state-changing request was actually intended by the user.
    /// </summary>
    /// <remarks>
    /// Lives here because the answer depends entirely on how the session travels. A bearer token is
    /// attached by the caller and therefore proves intent by itself; a cookie is attached by the
    /// browser whoever asked, so it does not. The transport that introduces the ambiguity is the one
    /// that has to resolve it — and it covers the controller endpoints, which an endpoint filter
    /// cannot reach.
    /// </remarks>
    Task<bool> IsRequestIntentionalAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
