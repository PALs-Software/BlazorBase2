using BlazorBase.Components.Services;

namespace AppTemplate.Tests.Infrastructure;

public class StubThemeService : IThemeService
{
    public ThemePreference CurrentTheme { get; private set; } = ThemePreference.System;

    public string? AccentColor => null;

    public string? NeutralBaseColor => null;

    public event Action? ThemeChanged;

    public Task InitializeAsync() => Task.CompletedTask;

    public Task ApplyAsync() => Task.CompletedTask;

    public Task SetThemeAsync(ThemePreference preference)
    {
        CurrentTheme = preference;
        ThemeChanged?.Invoke();

        return Task.CompletedTask;
    }
}
