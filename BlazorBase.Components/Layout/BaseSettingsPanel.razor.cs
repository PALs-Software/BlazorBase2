using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Layout;

/// <summary>
/// The settings drawer of the application shell: slides in from the end edge over a dimmed page and holds the
/// language and the theme, followed by whatever sections the host adds as <see cref="ChildContent"/>.
/// </summary>
/// <remarks>
/// It behaves as a modal dialog: the focus moves into it when it opens and cycles inside it, Escape and a click
/// on the dimmed page close it. <see cref="BaseLayout"/> renders one behind its settings button, so a host
/// normally only supplies <see cref="BaseLayout.SettingsContent"/>; the component also works on its own.
/// The language and theme are applied immediately, there is nothing to save.
/// </remarks>
public partial class BaseSettingsPanel(
    IThemeService themeService,
    ILanguageService languageService,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseSettingsPanel> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly IThemeService ThemeService = themeService;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseSettingsPanel> Localizer = localizer;

    #endregion

    /// <summary>The languages offered, as code and display name; the display names are not translated. Empty hides the language section.</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> DefaultLanguageOptions { get; } =
        [new("en", "English"), new("de", "Deutsch")];

    [Parameter]
    public bool IsOpen { get; set; }

    /// <summary>Raised when the panel asks to be closed (close button, Escape, click on the dimmed page).</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>The languages offered. Null offers <see cref="DefaultLanguageOptions"/>.</summary>
    [Parameter]
    public IReadOnlyList<KeyValuePair<string, string>>? LanguageOptions { get; set; }

    /// <summary>
    /// Raised with the chosen language code instead of changing the language directly, so a host can store the
    /// choice (e.g. on the account) before it calls <see cref="ILanguageService.SetLanguageAsync"/> itself.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnLanguageSelected { get; set; }

    /// <summary>Id of the panel element, for the button that opens it to point <c>aria-controls</c> at.</summary>
    [Parameter]
    public string PanelId { get; set; } = "base-settings";

    /// <summary>Host sections shown below the language and theme.</summary>
    [Parameter]
    public RenderFragment<SettingsPanelContext>? ChildContent { get; set; }

    private IJSObjectReference? Module;
    private ElementReference PanelElement;
    private bool IsFocusPending;
    private bool WasOpen;
    private string TitleId => PanelId + "-title";

    private SettingsPanelContext Context => PanelContext ??= new SettingsPanelContext(CloseAsync);

    private SettingsPanelContext? PanelContext;

    private IReadOnlyList<KeyValuePair<string, string>> LanguageChoices => LanguageOptions ?? DefaultLanguageOptions;

    protected override void OnInitialized()
    {
        ThemeService.ThemeChanged += OnThemeChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnParametersSet()
    {
        if (!IsOpen)
        {
            IsFocusPending = false;
            WasOpen = false;
            return;
        }

        if (WasOpen)
            return;

        WasOpen = true;
        IsFocusPending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!IsFocusPending)
            return;

        IsFocusPending = false;
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorBase.Components/js/layout.js");
        await Module.InvokeVoidAsync("trapFocus", PanelElement);
    }

    private Task CloseAsync() => OnClose.InvokeAsync();

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseAsync();
    }

    private async Task SelectLanguageAsync(string? code)
    {
        if (string.IsNullOrEmpty(code) || code == LanguageService.CurrentLanguage)
            return;

        if (OnLanguageSelected.HasDelegate)
        {
            await OnLanguageSelected.InvokeAsync(code);
            return;
        }

        await LanguageService.SetLanguageAsync(code);
    }

    private async Task SelectThemeAsync(string? value)
    {
        if (!Enum.TryParse<ThemePreference>(value, out var preference) || preference == ThemeService.CurrentTheme)
            return;

        await ThemeService.SetThemeAsync(preference);
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public async ValueTask DisposeAsync()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;

        if (Module is not null)
        {
            try
            {
                await Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            Module = null;
        }

        GC.SuppressFinalize(this);
    }
}
