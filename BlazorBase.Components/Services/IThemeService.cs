namespace BlazorBase.Components.Services;

public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public interface IThemeService
{
    ThemePreference CurrentTheme { get; }
    event Action? ThemeChanged;
    Task SetThemeAsync(ThemePreference preference);
    Task InitializeAsync();
}
