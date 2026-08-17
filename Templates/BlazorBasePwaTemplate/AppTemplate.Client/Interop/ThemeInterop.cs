using BlazorBase.User.Services;
using Microsoft.JSInterop;

namespace AppTemplate.Client.Interop;

public class ThemeInterop(IJSRuntime jsRuntime) : IThemeInterop
{
    #region Injects
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private IJSObjectReference? Module;

    private async Task<IJSObjectReference> GetModuleAsync()
        => Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");

    public async Task ApplyAsync(ThemePreference preference)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("apply", preference.ToString().ToLowerInvariant());
    }

    public async Task<bool> PrefersDarkAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<bool>("prefersDark");
    }

    public async Task<string> ReadTokenAsync(string name)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string>("readToken", name);
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        if (Module is null)
            return;

        try
        {
            await Module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
