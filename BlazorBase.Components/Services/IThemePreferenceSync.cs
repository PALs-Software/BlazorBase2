namespace BlazorBase.Components.Services;

/// <summary>
/// Carries the light/dark choice to wherever the signed-in user's profile lives, so it follows them to
/// another browser or device. <see cref="ThemeService"/> calls it after every explicit choice; the
/// shell package has no account concept, so a host or the user package supplies the implementation.
/// </summary>
public interface IThemePreferenceSync
{
    /// <summary>
    /// Stores <paramref name="preference"/> on the signed-in user's account. Does nothing for an anonymous
    /// visitor and must not throw when the server cannot be reached: the choice is already applied locally.
    /// </summary>
    Task SaveAsync(ThemePreference preference);
}
