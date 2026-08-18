using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Extensions.Localization;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Components;

public partial class UserPreferencesPanel(
    IUserService userService,
    IAuthService authService,
    ITokenStorage tokenStorage,
    BlazorBaseUserAuthStateProvider authStateProvider,
    IThemeService themeService,
    ILanguageService languageService,
    IFormFactor formFactor,
    IAppConfigService appConfigService,
    IStringLocalizer<UserPreferencesPanel> localizer)
{
    #region Injects

    private readonly IUserService UserService = userService;
    private readonly IAuthService AuthService = authService;
    private readonly ITokenStorage TokenStorage = tokenStorage;
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;
    private readonly IThemeService ThemeService = themeService;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IFormFactor FormFactor = formFactor;
    private readonly IAppConfigService AppConfigService = appConfigService;
    private readonly IStringLocalizer<UserPreferencesPanel> Localizer = localizer;

    #endregion

    private string SelectedTheme = "System";
    private string SelectedLanguage = "en";
    private string ServerUrl = string.Empty;
    private bool Saving;
    private bool TestingConnection;
    private bool ConnectionTestOk;
    private string? SaveMessage;
    private string? ConnectionTestResult;

    private List<KeyValuePair<string, string>> ThemeDisplayOptions = [];
    private List<KeyValuePair<string, string>> LanguageDisplayOptions = [];

    private bool ShowServerUrl =>
        FormFactor.GetFormFactor() is "Mobile" or "Desktop";

    protected override async Task OnInitializedAsync()
    {
        ThemeDisplayOptions =
        [
            new("System", Localizer["System"]),
            new("Light", Localizer["Light"]),
            new("Dark", Localizer["Dark"]),
        ];
        LanguageDisplayOptions =
        [
            new("en", Localizer["English"]),
            new("de", Localizer["German"]),
        ];

        try
        {
            var profile = await UserService.GetMeAsync();
            SelectedTheme = profile.ThemePreference;
            SelectedLanguage = profile.Language;
        }
        catch
        {
            SelectedTheme = ThemeService.CurrentTheme.ToString();
            SelectedLanguage = LanguageService.CurrentLanguage;
        }

        if (await AppConfigService.IsConfiguredAsync())
            ServerUrl = await AppConfigService.GetServerUrlAsync() ?? string.Empty;
    }

    async Task SaveSettings()
    {
        Saving = true;
        SaveMessage = null;

        var themePref = SelectedTheme switch
        {
            "Light" => ThemePreference.Light,
            "Dark" => ThemePreference.Dark,
            _ => ThemePreference.System,
        };
        await ThemeService.SetThemeAsync(themePref);

        var language = SelectedLanguage;
        await UserService.UpdateSettingsAsync(new UpdateUserSettingsRequest
        {
            ThemePreference = SelectedTheme,
            Language = language,
        });

        var tokens = await TokenStorage.GetTokensAsync();
        if (tokens is not null && !string.IsNullOrEmpty(tokens.RefreshToken))
        {
            var newTokens = await AuthService.RefreshAsync(tokens.RefreshToken);
            await AuthStateProvider.MarkUserAsAuthenticated(newTokens);
        }

        await LanguageService.SetLanguageAsync(language);

        if (!string.IsNullOrWhiteSpace(ServerUrl))
            await AppConfigService.SaveServerUrlAsync(ServerUrl.TrimEnd('/'));

        SaveMessage = Localizer["SettingsSaved"];
        Saving = false;
    }

    async Task TestConnection()
    {
        TestingConnection = true;
        ConnectionTestResult = null;

        try
        {
            var testUrl = ServerUrl.TrimEnd('/') + "/api/auth/status";
            using var client = new HttpClient();
            var response = await client.GetAsync(testUrl);
            ConnectionTestOk = response.IsSuccessStatusCode;
            ConnectionTestResult = ConnectionTestOk ? Localizer["ConnectionSuccessful"] : string.Format(Localizer["ServerReturned"], response.StatusCode);
        }
        catch (Exception ex)
        {
            ConnectionTestOk = false;
            ConnectionTestResult = string.Format(Localizer["ConnectionFailed"], ex.Message);
        }

        TestingConnection = false;
    }
}
