using BlazorBase.Components.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;

namespace BlazorBase.Components.Layout;

/// <summary>
/// Cycles the application between following the operating system, light and dark.
/// </summary>
/// <remarks>
/// Three states rather than a plain on/off switch, because "system" is a real preference and a
/// two-way toggle can only offer it by guessing what the system currently resolves to. Every host
/// that wants the choice in its header would otherwise rebuild this, as two of them already had.
/// </remarks>
public partial class BaseThemeToggle(IThemeService themeService, IStringLocalizer<BaseThemeToggle> localizer)
    : ComponentBase, IDisposable
{
    #region Injects

    private readonly IThemeService ThemeService = themeService;
    private readonly IStringLocalizer<BaseThemeToggle> Localizer = localizer;

    #endregion

    private Icon Icon => ThemeService.CurrentTheme switch
    {
        ThemePreference.Light => new Size20.WeatherSunny(),
        ThemePreference.Dark => new Size20.WeatherMoon(),
        _ => new Size20.Desktop(),
    };

    private string Title => ThemeService.CurrentTheme switch
    {
        ThemePreference.Light => Localizer["SwitchToDark"],
        ThemePreference.Dark => Localizer["SwitchToSystem"],
        _ => Localizer["SwitchToLight"],
    };

    protected override void OnInitialized() => ThemeService.ThemeChanged += OnThemeChanged;

    private Task CycleAsync() => ThemeService.SetThemeAsync(ThemeService.CurrentTheme switch
    {
        ThemePreference.System => ThemePreference.Light,
        ThemePreference.Light => ThemePreference.Dark,
        _ => ThemePreference.System,
    });

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose() => ThemeService.ThemeChanged -= OnThemeChanged;
}
