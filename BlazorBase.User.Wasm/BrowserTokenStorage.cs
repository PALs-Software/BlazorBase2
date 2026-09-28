using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.JSInterop;

namespace BlazorBase.User.Wasm;

/// <summary>
/// Persists both tokens in <c>localStorage</c>, surviving a browser reload.
/// </summary>
/// <remarks>
/// Not the default — see <see cref="BlazorBaseUserWasmServiceCollectionExtensions.AddBlazorBaseUserWasm"/>.
/// <c>localStorage</c> is readable by any script running on the origin, so this trades that exposure
/// for a session that survives a reload; register it only through the explicit
/// <c>persistTokensInBrowserStorage</c> opt-in, and prefer the cookie transport in a browser instead.
/// </remarks>
public class BrowserTokenStorage(IJSRuntime jsRuntime) : ITokenStorage
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string StorageKey = "blazorbase_auth_tokens";

    public async Task<LoginResponse?> GetTokensAsync()
    {
        var json = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<LoginResponse>(json);
    }

    public async Task SaveTokensAsync(LoginResponse tokens)
    {
        var json = JsonSerializer.Serialize(tokens);
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
    }

    public async Task ClearTokensAsync()
    {
        await JsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
