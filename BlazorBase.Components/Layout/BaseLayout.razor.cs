using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Layout;

public partial class BaseLayout(IThemeService themeService, ILanguageService languageService, IStringLocalizer<BaseLayout> localizer) : ComponentBase, IDisposable
{
    #region Injects

    private readonly IThemeService ThemeService = themeService;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IStringLocalizer<BaseLayout> Localizer = localizer;

    #endregion

    [Parameter]
    public string AppTitle { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? Navigation { get; set; }

    [Parameter]
    public RenderFragment? Banner { get; set; }

    [Parameter]
    public RenderFragment? HeaderContent { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    private DesignThemeModes CurrentMode => ThemeService.CurrentTheme switch
    {
        ThemePreference.Light => DesignThemeModes.Light,
        ThemePreference.Dark => DesignThemeModes.Dark,
        _ => DesignThemeModes.System,
    };

    protected override async Task OnInitializedAsync()
    {
        await ThemeService.InitializeAsync();
        await LanguageService.InitializeAsync();
        ThemeService.ThemeChanged += OnThemeChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await ThemeService.ApplyAsync();
        StateHasChanged();
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    private void OnLanguageChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        ThemeService.ThemeChanged -= OnThemeChanged;
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }
}
