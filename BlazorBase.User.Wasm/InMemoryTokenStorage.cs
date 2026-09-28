using BlazorBase.User.Models;
using BlazorBase.User.Services;

namespace BlazorBase.User.Wasm;

/// <summary>
/// Holds both tokens in memory only, for the lifetime of the scope.
/// </summary>
/// <remarks>
/// The default for <see cref="BlazorBaseUserWasmServiceCollectionExtensions.AddBlazorBaseUserWasm"/>.
/// A browser reload loses this storage entirely — including the refresh token — so a reload signs a
/// bearer-mode user out. That is the correct trade-off for a browser: nothing a script can read
/// survives a reload either, and it is the reason the cookie transport
/// (<see cref="BlazorBaseUserWasmServiceCollectionExtensions.AddBlazorBaseUserWasmCookieAuth"/>) is the
/// recommended mode for one. Use <see cref="BrowserTokenStorage"/> only when that trade-off is
/// unacceptable and the exposure of a script-readable token is the lesser risk.
/// </remarks>
public sealed class InMemoryTokenStorage : ITokenStorage
{
    private LoginResponse? _tokens;

    public Task<LoginResponse?> GetTokensAsync() => Task.FromResult(_tokens);

    public Task SaveTokensAsync(LoginResponse tokens)
    {
        _tokens = tokens;
        return Task.CompletedTask;
    }

    public Task ClearTokensAsync()
    {
        _tokens = null;
        return Task.CompletedTask;
    }
}
