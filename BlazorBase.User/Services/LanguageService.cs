using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorBase.User.Services;

/// <summary>
/// Holds the language the app is running in and applies it to the current thread's culture.
/// </summary>
/// <remarks>
/// Under WebAssembly the runtime decides which satellite resource assemblies to download while the
/// host starts. Changing <see cref="CultureInfo.DefaultThreadCurrentUICulture"/> afterwards
/// therefore changes nothing visible — the resources were never fetched and
/// <c>IStringLocalizer</c> keeps falling back to the neutral language. A runtime switch only takes
/// effect after a full page load, which is why <see cref="SetLanguageAsync"/> triggers one.
/// </remarks>
public class LanguageService(AuthenticationStateProvider authStateProvider, NavigationManager navigationManager) : ILanguageService
{
    #region Injects
    private readonly AuthenticationStateProvider AuthStateProvider = authStateProvider;
    private readonly NavigationManager NavigationManager = navigationManager;
    #endregion

    private const string DefaultLanguage = "en";

    public string CurrentLanguage { get; private set; } = DefaultLanguage;
    public event Action? LanguageChanged;

    private string StartupLanguage = DefaultLanguage;

    private bool StartupLanguageApplied;

    public void ApplyStartupLanguage(string cultureCode)
    {
        CurrentLanguage = cultureCode;
        StartupLanguage = cultureCode;
        StartupLanguageApplied = true;

        ApplyCulture(cultureCode);
    }

    public async Task InitializeAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var languageClaim = authState.User.FindFirst("language")?.Value;

        if (string.IsNullOrEmpty(languageClaim) || languageClaim == CurrentLanguage)
        {
            ApplyCulture(CurrentLanguage);
            return;
        }

        await SetLanguageAsync(languageClaim);
    }

    public Task SetLanguageAsync(string cultureCode)
    {
        var languageChanged = cultureCode != CurrentLanguage;

        CurrentLanguage = cultureCode;
        ApplyCulture(cultureCode);

        if (!languageChanged)
            return Task.CompletedTask;

        LanguageChanged?.Invoke();

        if (RequiresReloadForResources(cultureCode))
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);

        return Task.CompletedTask;
    }

    /// <summary>
    /// True when the requested language's resources cannot be present in this process, which is the
    /// case in the browser as soon as the language differs from the one the host started in.
    /// </summary>
    /// <remarks>
    /// Guarded by <see cref="StartupLanguageApplied"/> so that a host which never called
    /// <see cref="BlazorBaseLanguageExtensions.UseBlazorBaseLanguageAsync"/> keeps its previous
    /// behaviour instead of reloading into the very same startup language over and over.
    /// </remarks>
    private bool RequiresReloadForResources(string cultureCode)
        => StartupLanguageApplied && OperatingSystem.IsBrowser() && cultureCode != StartupLanguage;

    private static void ApplyCulture(string cultureCode)
    {
        var culture = new CultureInfo(cultureCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
