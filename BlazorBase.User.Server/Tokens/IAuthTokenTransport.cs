using BlazorBase.User.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Decides how an issued token pair reaches the client, and where it is read back from.
/// </summary>
/// <remarks>
/// Two shapes of client cannot share one answer. A browser is best served by <c>HttpOnly</c> cookies
/// it cannot read, because anything JavaScript can read, an XSS can read too. A native or
/// service-to-service client has no cookie jar worth the name and needs the token in hand. The token
/// pair itself is identical either way — only its delivery differs, so this seam sits after
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
    /// </remarks>
    Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the refresh token for a rotation request. <paramref name="fromRequestBody"/> is
    /// whatever the caller sent, which a cookie-based implementation is free to ignore.
    /// </summary>
    Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default);

    /// <summary>Removes anything <see cref="WriteAsync"/> left on the client. Called on logout.</summary>
    Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}
