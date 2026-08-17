using BlazorBase.User.Services;

namespace AppTemplate.Client.Interop;

public interface IThemeInterop : IAsyncDisposable
{
    Task ApplyAsync(ThemePreference preference);

    Task<bool> PrefersDarkAsync();

    Task<string> ReadTokenAsync(string name);
}
