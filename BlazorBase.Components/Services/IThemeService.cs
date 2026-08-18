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

    /// <summary>
    /// Reads the preference without touching the document, so it can run before the first render.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Writes the current preference onto the document. Call it once the first render has happened —
    /// it uses JS interop, which is not available earlier.
    /// </summary>
    Task ApplyAsync();

    Task SetThemeAsync(ThemePreference preference);
}
