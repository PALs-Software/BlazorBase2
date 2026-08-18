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
///
/// Only <see cref="SetThemeAsync"/> writes to local storage. Writing on every load would persist what
/// the profile seeded, and that value would then outrank the profile from then on — changing the
/// preference on another device would never reach this browser again.
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

        var adopted = false;

        if (stored is not null && Enum.TryParse<ThemePreference>(stored, true, out var parsed) && parsed != CurrentTheme)
        {
            CurrentTheme = parsed;
            adopted = true;
        }

        await module.InvokeVoidAsync("apply", CurrentTheme.ToString(), false);
        await ReadAccentAsync(module);

        if (adopted)
            ThemeChanged?.Invoke();
    }

    public async Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;

        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("apply", preference.ToString(), true);
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
        GC.SuppressFinalize(this);

        if (Module is null)
            return;

        try
        {
            await Module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit or web view is already gone; there is nothing left to release on the other side.
        }

        Module = null;
    }
}
