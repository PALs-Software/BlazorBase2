using BlazorBase.User.Services;
using Microsoft.Maui.Storage;

namespace BlazorBase.User.Maui;

public class MauiAppConfigService : IAppConfigService
{
    private const string ServerUrlKey = "blazorbase_server_url";

    public async Task<string?> GetServerUrlAsync()
    {
        return await SecureStorage.Default.GetAsync(ServerUrlKey).ConfigureAwait(false);
    }

    public async Task SaveServerUrlAsync(string url)
    {
        await SecureStorage.Default.SetAsync(ServerUrlKey, url.TrimEnd('/')).ConfigureAwait(false);
    }

    public async Task<bool> IsConfiguredAsync()
    {
        var url = await GetServerUrlAsync().ConfigureAwait(false);
        return !string.IsNullOrEmpty(url);
    }
}
