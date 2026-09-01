using System.Security.Claims;
using BlazorBase.User.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Returns the Identity bearer token pair in the response body and expects the refresh token back in
/// the request body. This is the framework's default transport and stays the default, so an
/// application that does not opt into cookies is unaffected.
/// </summary>
/// <remarks>
/// Appropriate for clients that are not a browser — a MAUI app, a CLI, another service. In a browser
/// it means the token is reachable from script; see <see cref="IdentityCookieAuthTransport"/> for the
/// alternative used when the client is a browser served from the same origin.
///
/// The token pair itself is already minted by <c>AuthTokenIssuer</c> before this class ever sees it —
/// this transport only decides where it travels, not how it is produced.
/// </remarks>
public sealed class IdentityBearerAuthTransport : IAuthTokenTransport
{
    public Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
        Task.FromResult(tokens);

    public Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default) =>
        Task.FromResult(fromRequestBody);

    public Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Always true. A bearer token has to be attached deliberately by the caller, so its presence is
    /// the proof of intent that a cookie cannot give.
    /// </summary>
    public Task<bool> IsRequestIntentionalAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
