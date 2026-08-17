using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Maui.Storage;

namespace BlazorBase.User.Maui;

public class SecureTokenStorage : ITokenStorage
{
    private const string StorageKey = "blazorbase_auth_tokens";

    public async Task<LoginResponse?> GetTokensAsync()
    {
        var json = await SecureStorage.Default.GetAsync(StorageKey).ConfigureAwait(false);
        if (string.IsNullOrEmpty(json))
            return null;

        return JsonSerializer.Deserialize<LoginResponse>(json);
    }

    public async Task SaveTokensAsync(LoginResponse tokens)
    {
        var json = JsonSerializer.Serialize(tokens);
        await SecureStorage.Default.SetAsync(StorageKey, json).ConfigureAwait(false);
    }

    public Task ClearTokensAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        return Task.CompletedTask;
    }
}
