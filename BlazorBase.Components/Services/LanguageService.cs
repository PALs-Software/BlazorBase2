using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Services;

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
public class LanguageService(AuthenticationStateProvider authStateProvider, NavigationManager navigationManager, IJSRuntime jsRuntime) : ILanguageService
{
    #region Injects
    private readonly AuthenticationStateProvider AuthStateProvider = authStateProvider;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    #endregion

    private const string DefaultLanguage = "en";

    public string CurrentLanguage { get; private set; } = DefaultLanguage;
    public event Action? LanguageChanged;

    private string StartupLanguage = DefaultLanguage;

    private bool StartupLanguageApplied;

    /// <summary>
    /// Adopts the language the app is starting in. The JS interop call this makes to stamp
    /// <c>&lt;html lang&gt;</c> is not awaited: this runs before <c>Build().RunAsync()</c>, and
    /// nothing is rendered yet for a user to notice the attribute catching up a moment later.
    /// </summary>
    public void ApplyStartupLanguage(string cultureCode)
    {
        CurrentLanguage = cultureCode;
        StartupLanguage = cultureCode;
        StartupLanguageApplied = true;

        ApplyCulture(cultureCode);
        _ = SetDocumentLanguageAsync(cultureCode);
    }

    public async Task InitializeAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var languageClaim = authState.User.FindFirst("language")?.Value;

        if (string.IsNullOrEmpty(languageClaim) || languageClaim == CurrentLanguage)
        {
            ApplyCulture(CurrentLanguage);
            await SetDocumentLanguageAsync(CurrentLanguage);
            return;
        }

        await SetLanguageAsync(languageClaim);
    }

    public async Task SetLanguageAsync(string cultureCode)
    {
        var languageChanged = cultureCode != CurrentLanguage;

        CurrentLanguage = cultureCode;
        ApplyCulture(cultureCode);
        await SetDocumentLanguageAsync(cultureCode);

        if (!languageChanged)
            return;

        LanguageChanged?.Invoke();

        if (RequiresReloadForResources(cultureCode))
            NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    }

    /// <summary>
    /// True when the requested language's resources cannot be present in this process, which is the
    /// case in the browser as soon as the language differs from the one the host started in.
    /// </summary>
    /// <remarks>
    /// Guarded by <see cref="StartupLanguageApplied"/> so that a host which never called
    /// <c>UseBlazorBaseLanguageAsync</c> keeps its previous
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

    /// <summary>
    /// Stamps <c>&lt;html lang&gt;</c> with the active language. Without this the document stays on
    /// whatever the static markup shipped (typically <c>en</c>) for the whole session, which is wrong
    /// for assistive technology and any other tooling that reads the attribute - a native browser
    /// control such as the built-in required-field validation bubble follows the browser's own UI
    /// language instead, so this intentionally does not attempt to affect that one.
    /// </summary>
    private async Task SetDocumentLanguageAsync(string cultureCode)
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("document.documentElement.setAttribute", "lang", cultureCode);
        }
        catch (JSException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
