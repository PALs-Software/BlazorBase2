namespace BlazorBase.Components.Services;

public interface ILanguageService
{
    string CurrentLanguage { get; }
    event Action? LanguageChanged;

    /// <summary>
    /// Changes the language at runtime. Under WebAssembly this reloads the page, because the
    /// satellite resource assemblies of the new language were never downloaded — see
    /// <see cref="BlazorBaseLanguageExtensions.UseBlazorBaseLanguageAsync"/>.
    /// </summary>
    Task SetLanguageAsync(string cultureCode);

    /// <summary>
    /// Adopts the language the app is starting in, without raising <see cref="LanguageChanged"/>
    /// and without reloading. Call this only from
    /// <see cref="BlazorBaseLanguageExtensions.UseBlazorBaseLanguageAsync"/>, before the host runs.
    /// </summary>
    void ApplyStartupLanguage(string cultureCode);

    Task InitializeAsync();
}
