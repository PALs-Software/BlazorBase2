using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.JSInterop;

namespace BlazorBase.User.Wasm;

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
