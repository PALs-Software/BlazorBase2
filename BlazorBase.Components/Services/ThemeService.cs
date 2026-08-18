using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.Components.Services;

public class ThemeService(AuthenticationStateProvider authStateProvider) : IThemeService
{
    #region Injects
    private readonly AuthenticationStateProvider AuthStateProvider = authStateProvider;
    #endregion

    public ThemePreference CurrentTheme { get; private set; } = ThemePreference.System;
    public event Action? ThemeChanged;

    public async Task InitializeAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var themeClaim = authState.User.FindFirst("themePreference")?.Value;
        if (themeClaim is not null && Enum.TryParse<ThemePreference>(themeClaim, true, out var parsed))
            CurrentTheme = parsed;
    }

    public Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;
        ThemeChanged?.Invoke();
        return Task.CompletedTask;
    }
}
