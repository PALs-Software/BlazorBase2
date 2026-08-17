using System.Security.Claims;
using AppTemplate.Client.Interop;
using AppTemplate.Client.Layout.Navigation;
using AppTemplate.Shared.Modules.Authentication;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace AppTemplate.Client.Layout;

public partial class MainLayout(
    IThemeService themeService,
    IThemeInterop themeInterop,
    AuthenticationStateProvider authenticationStateProvider,
    IStringLocalizer<MainLayout> localizer) : LayoutComponentBase, IDisposable
{
    #region Injects
    private readonly IThemeService ThemeService = themeService;
    private readonly IThemeInterop ThemeInterop = themeInterop;
    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;
    private readonly IStringLocalizer<MainLayout> Localizer = localizer;
    #endregion

    private const string AccentTokenName = "--color-accent";

    private IReadOnlyList<NavigationItem> NavigationItems = [];

    private bool IsDarkTheme;

    private DesignThemeModes FluentThemeMode = DesignThemeModes.System;

    private string? FluentAccentColor;

    private bool IsAuthenticated;

    private string UserInitials = string.Empty;

    private string ThemeToggleTitle => IsDarkTheme ? Localizer["SwitchToLight"] : Localizer["SwitchToDark"];

    protected override async Task OnInitializedAsync()
    {
        ThemeService.ThemeChanged += OnThemeChanged;
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;

        await ThemeService.InitializeAsync();
        await ApplyThemeAsync();
        await ReadAuthenticationStateAsync();
    }

    private async Task ReadAuthenticationStateAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        IsAuthenticated = user.Identity?.IsAuthenticated == true;
        NavigationItems = BuildNavigationItems(user);
        UserInitials = ResolveUserInitials(user);
    }

    private IReadOnlyList<NavigationItem> BuildNavigationItems(ClaimsPrincipal user)
    {
        List<NavigationItem> items =
        [
            new NavigationItem(Localizer["NavHome"], string.Empty, NavLinkMatch.All),
            new NavigationItem(Localizer["NavNotes"], "notes")
        ];

        if (user.IsInRole(RoleConstants.Admin))
            items.Add(new NavigationItem(Localizer["NavAdmin"], "admin/users"));

        return items;
    }

    private static string ResolveUserInitials(ClaimsPrincipal user)
    {
        var displayName = user.FindFirst("displayName")?.Value;

        if (string.IsNullOrWhiteSpace(displayName))
            return "?";

        var parts = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : string.Concat(parts[0][..1], parts[^1][..1]).ToUpperInvariant();
    }

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask)
        => InvokeAsync(async () =>
        {
            await ReadAuthenticationStateAsync();
            StateHasChanged();
        });

    private async Task ApplyThemeAsync()
    {
        IsDarkTheme = ThemeService.CurrentTheme switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => await ThemeInterop.PrefersDarkAsync()
        };

        await ThemeInterop.ApplyAsync(ThemeService.CurrentTheme);

        FluentThemeMode = IsDarkTheme ? DesignThemeModes.Dark : DesignThemeModes.Light;
        FluentAccentColor = await ThemeInterop.ReadTokenAsync(AccentTokenName);
    }

    private async Task ToggleThemeAsync()
    {
        var next = IsDarkTheme ? ThemePreference.Light : ThemePreference.Dark;
        await ThemeService.SetThemeAsync(next);
        await ApplyThemeAsync();
    }

    private void OnThemeChanged() => InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        ThemeService.ThemeChanged -= OnThemeChanged;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
    }
}
