using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Services;

/// <summary>
/// Keeps the light/dark preference and puts it on the document, where the token stylesheet picks it up.
/// </summary>
/// <remarks>
/// A signed-in user carries the preference in their profile, which seeds a browser that has no choice
/// of its own — that way the setting follows them to a new device. An explicit choice made here wins
/// afterwards, because local storage is only ever written by <see cref="SetThemeAsync"/>: letting the
/// profile claim win instead meant clicking the toggle appeared to work and silently reverted on the
/// next reload, since the claim still carried the old value until the profile itself was saved.
/// </remarks>
public class ThemeService(AuthenticationStateProvider authenticationStateProvider, IJSRuntime jsRuntime) : IThemeService, IAsyncDisposable
{
    #region Injects

    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;
    private readonly IJSRuntime JsRuntime = jsRuntime;

    #endregion

    private IJSObjectReference? Module;

    public ThemePreference CurrentTheme { get; private set; } = ThemePreference.System;

    public string? AccentColor { get; private set; }

    public event Action? ThemeChanged;

    public async Task InitializeAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var claim = authenticationState.User.FindFirst("themePreference")?.Value;

        if (claim is not null && Enum.TryParse<ThemePreference>(claim, true, out var parsed))
            CurrentTheme = parsed;
    }

    public async Task ApplyAsync()
    {
        var module = await LoadModuleAsync();
        var stored = await module.InvokeAsync<string?>("stored");

        if (stored is not null && Enum.TryParse<ThemePreference>(stored, true, out var parsed) && parsed != CurrentTheme)
        {
            CurrentTheme = parsed;
            ThemeChanged?.Invoke();
        }

        await module.InvokeVoidAsync("apply", CurrentTheme.ToString());
        await ReadAccentAsync(module);
    }

    public async Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;

        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("apply", preference.ToString());
        await ReadAccentAsync(module);

        ThemeChanged?.Invoke();
    }

    private async Task ReadAccentAsync(IJSObjectReference module)
    {
        var accent = await module.InvokeAsync<string?>("readToken", "--color-accent");
        AccentColor = string.IsNullOrWhiteSpace(accent) ? null : accent;
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
