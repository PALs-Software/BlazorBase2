using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Stores nothing. The transport that owns the session keeps it somewhere the client cannot reach —
/// an <c>HttpOnly</c> cookie — so there is no token to hold and nothing to clear.
/// </summary>
/// <remarks>
/// Deliberately not a partial implementation: returning <see langword="null"/> from
/// <see cref="GetTokensAsync"/> is the correct answer under the cookie transport, not a gap. Anything
/// that needs the claims asks the server through <c>GET /api/auth/session</c>.
/// </remarks>
public sealed class NoOpTokenStorage : ITokenStorage
{
    public Task<LoginResponse?> GetTokensAsync() => Task.FromResult<LoginResponse?>(null);

    public Task SaveTokensAsync(LoginResponse tokens) => Task.CompletedTask;

    public Task ClearTokensAsync() => Task.CompletedTask;
}
