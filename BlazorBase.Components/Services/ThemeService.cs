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
    private DotNetObjectReference<ThemeService>? SelfReference;

    public ThemePreference CurrentTheme { get; private set; } = ThemePreference.System;

    public string? AccentColor { get; private set; }

    public string? NeutralBaseColor { get; private set; }

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
        await ReadSeedColorsAsync(module);
        await WatchSystemAsync(module);

        if (adopted)
            ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Called from the browser when the operating system switches between light and dark.
    /// </summary>
    /// <remarks>
    /// Only the <see cref="ThemePreference.System"/> preference follows along; an explicit choice
    /// stamps the root element and is unaffected by what the operating system does. The stylesheet
    /// repaints on its own either way — what needs re-reading is everything derived from the tokens on
    /// this side, which is why the accent is fetched again and the layout asked to render.
    /// </remarks>
    [JSInvokable]
    public async Task OnSystemThemeChangedAsync()
    {
        if (CurrentTheme != ThemePreference.System)
            return;

        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("apply", CurrentTheme.ToString(), false);
        await ReadSeedColorsAsync(module);

        ThemeChanged?.Invoke();
    }

    private async Task WatchSystemAsync(IJSObjectReference module)
    {
        if (SelfReference is not null)
            return;

        SelfReference = DotNetObjectReference.Create(this);
        await module.InvokeVoidAsync("watchSystem", SelfReference);
    }

    public async Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;

        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("apply", preference.ToString(), true);
        await ReadSeedColorsAsync(module);

        ThemeChanged?.Invoke();
    }

    /// <summary>
    /// Reads the two values FluentUI's own design system needs, so its controls end up in the same
    /// palette as everything styled from the tokens directly.
    /// </summary>
    private async Task ReadSeedColorsAsync(IJSObjectReference module)
    {
        AccentColor = await ReadColorAsync(module, "--color-accent");
        NeutralBaseColor = await ReadColorAsync(module, "--color-neutral-base");
    }

    private static async Task<string?> ReadColorAsync(IJSObjectReference module, string token)
    {
        var value = await module.InvokeAsync<string?>("readToken", token);
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
            await Module.InvokeVoidAsync("unwatchSystem");
            await Module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit or web view is already gone; there is nothing left to release on the other side.
        }

        Module = null;
        SelfReference?.Dispose();
        SelfReference = null;
    }
}
