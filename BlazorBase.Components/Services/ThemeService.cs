using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Services;

/// <summary>
/// Keeps the light/dark preference and puts it on the document, where the token stylesheet picks it up.
/// </summary>
/// <remarks>
/// A signed-in user carries the preference in their profile, so the claim wins when there is one — that
/// way the choice follows them to another device. Everyone else falls back to what the browser stored,
/// which is also what keeps the choice across a reload; before this existed the preference lived only
/// in memory and every refresh silently reverted it.
/// </remarks>
public class ThemeService(AuthenticationStateProvider authenticationStateProvider, IJSRuntime jsRuntime) : IThemeService, IAsyncDisposable
{
    #region Injects

    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;
    private readonly IJSRuntime JsRuntime = jsRuntime;

    #endregion

    private IJSObjectReference? Module;
    private bool PreferenceCameFromProfile;

    public ThemePreference CurrentTheme { get; private set; } = ThemePreference.System;

    public event Action? ThemeChanged;

    public async Task InitializeAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var claim = authenticationState.User.FindFirst("themePreference")?.Value;

        if (claim is null || !Enum.TryParse<ThemePreference>(claim, true, out var parsed))
            return;

        CurrentTheme = parsed;
        PreferenceCameFromProfile = true;
    }

    public async Task ApplyAsync()
    {
        var module = await LoadModuleAsync();

        if (!PreferenceCameFromProfile)
        {
            var stored = await module.InvokeAsync<string?>("stored");

            if (stored is not null && Enum.TryParse<ThemePreference>(stored, true, out var parsed) && parsed != CurrentTheme)
            {
                CurrentTheme = parsed;
                ThemeChanged?.Invoke();
            }
        }

        await module.InvokeVoidAsync("apply", CurrentTheme.ToString());
    }

    public async Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;

        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("apply", preference.ToString());

        ThemeChanged?.Invoke();
    }

    private async Task<IJSObjectReference> LoadModuleAsync()
    {
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/theme.js");

        return Module;
    }

    public async ValueTask DisposeAsync()
    {
        if (Module is null)
            return;

        await Module.DisposeAsync();
        Module = null;
        GC.SuppressFinalize(this);
    }
}
