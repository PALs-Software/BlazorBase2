namespace BlazorBase.Components.Services;

public interface IThemeService
{
    ThemePreference CurrentTheme { get; }

    /// <summary>
    /// The accent the active theme resolves to, for handing on to FluentUI's design system so its own
    /// components adopt the palette instead of staying on their default blue. Null until
    /// <see cref="ApplyAsync"/> has run.
    /// </summary>
    string? AccentColor { get; }

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
