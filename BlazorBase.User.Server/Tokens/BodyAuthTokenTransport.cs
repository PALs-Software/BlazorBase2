using BlazorBase.User.Models;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Returns the token pair in the response body and expects it back in the request body. This is the
/// framework's original behaviour and stays the default, so an application that does not opt into
/// cookies is unaffected.
/// </summary>
/// <remarks>
/// Appropriate for clients that are not a browser — a MAUI app, a CLI, another service. In a browser
/// it means the token is reachable from script; see <see cref="CookieAuthTokenTransport"/> for why
/// that is worth avoiding when the client is a browser served from the same origin.
/// </remarks>
public sealed class BodyAuthTokenTransport : IAuthTokenTransport
{
    public Task<LoginResponse> WriteAsync(HttpContext httpContext, LoginResponse tokens, CancellationToken cancellationToken = default) =>
        Task.FromResult(tokens);

    public Task<string?> ReadRefreshTokenAsync(HttpContext httpContext, string? fromRequestBody, CancellationToken cancellationToken = default) =>
        Task.FromResult(fromRequestBody);

    public Task ClearAsync(HttpContext httpContext, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
