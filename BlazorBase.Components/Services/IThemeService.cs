namespace BlazorBase.Components.Services;

public interface IThemeService
{
    ThemePreference CurrentTheme { get; }

    /// <summary>
    /// The accent the active theme resolves to (<c>--color-accent</c>), for code that needs the colour on this
    /// side, such as a chart. Null until <see cref="ApplyAsync"/> has run.
    /// </summary>
    string? AccentColor { get; }

    /// <summary>
    /// The neutral base of the active theme (<c>--color-neutral-base</c>), the swatch the disabled and skeleton
    /// colours are tinted from. Null until <see cref="ApplyAsync"/> has run.
    /// </summary>
    string? NeutralBaseColor { get; }

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

    /// <summary>
    /// Applies the preference, remembers it in the browser and, for a signed-in user, stores it on the account
    /// so it follows them to other devices.
    /// </summary>
    Task SetThemeAsync(ThemePreference preference);

    /// <summary>
    /// Like <see cref="SetThemeAsync(ThemePreference)"/>, but lets a caller that saves the account profile itself
    /// skip the account write. An implementation without an account concept ignores the flag.
    /// </summary>
    Task SetThemeAsync(ThemePreference preference, bool persistToAccount) => SetThemeAsync(preference);
}
