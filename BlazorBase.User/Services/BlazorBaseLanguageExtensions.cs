using System.Globalization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlazorBase.User.Services;

public static class BlazorBaseLanguageExtensions
{
    private static readonly string[] DefaultSupportedLanguages = ["en", "de"];

    /// <summary>
    /// Resolves the language the app should start in — the signed-in account's persisted claim, or
    /// the client system's own language when there is none — and applies it before the host runs.
    /// </summary>
    /// <remarks>
    /// This has to happen before <c>RunAsync()</c>. Blazor WebAssembly picks the satellite resource
    /// assemblies it downloads while the host starts, so a language chosen afterwards leaves the
    /// app with the neutral resources and no visible change at all.
    /// <para>
    /// Call it after <c>builder.Build()</c> and after
    /// <see cref="BlazorBaseDevelopmentSessionExtensions.UseBlazorBaseDevelopmentSessionAsync"/>,
    /// whose session is what carries the language claim while developing:
    /// <code>
    /// var host = builder.Build();
    /// await host.Services.UseBlazorBaseDevelopmentSessionAsync();
    /// await host.Services.UseBlazorBaseLanguageAsync("en", "de");
    /// await host.RunAsync();
    /// </code>
    /// </para>
    /// </remarks>
    /// <param name="supportedLanguages">
    /// The two-letter codes the app ships resources for, most preferred first; the first one is the
    /// fallback for anything else. Defaults to the languages the framework's own resources cover.
    /// </param>
    public static async Task UseBlazorBaseLanguageAsync(this IServiceProvider serviceProvider, params string[] supportedLanguages)
    {
        var languageService = serviceProvider.GetService<ILanguageService>();

        if (languageService is null)
            return;

        var supported = supportedLanguages.Length > 0 ? supportedLanguages : DefaultSupportedLanguages;
        var logger = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger("BlazorBase.Language");

        try
        {
            var persistedLanguage = await ResolvePersistedLanguageAsync(serviceProvider);

            languageService.ApplyStartupLanguage(ConstrainToSupported(persistedLanguage ?? CultureInfo.CurrentCulture.Name, supported));
        }
        catch (Exception exception)
        {
            // Never keep the app from starting over a language lookup — fall back to the default.
            logger?.LogWarning(exception, "The startup language could not be resolved; starting in {Language}.", supported[0]);
            languageService.ApplyStartupLanguage(supported[0]);
        }
    }

    private static async Task<string?> ResolvePersistedLanguageAsync(IServiceProvider serviceProvider)
    {
        var authenticationStateProvider = serviceProvider.GetService<AuthenticationStateProvider>();

        if (authenticationStateProvider is null)
            return null;

        var authenticationState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var languageClaim = authenticationState.User.FindFirst("language")?.Value;

        return string.IsNullOrWhiteSpace(languageClaim) ? null : languageClaim;
    }

    /// <summary>
    /// Reduces a culture tag such as <c>de-DE</c> to the two-letter code the app has resources for,
    /// falling back to the first supported language for everything it does not cover.
    /// </summary>
    private static string ConstrainToSupported(string cultureTag, string[] supportedLanguages)
    {
        try
        {
            var twoLetterLanguage = CultureInfo.GetCultureInfo(cultureTag, predefinedOnly: true).TwoLetterISOLanguageName;

            return supportedLanguages.FirstOrDefault(language => string.Equals(language, twoLetterLanguage, StringComparison.OrdinalIgnoreCase))
                ?? supportedLanguages[0];
        }
        catch (CultureNotFoundException)
        {
            return supportedLanguages[0];
        }
    }
}
