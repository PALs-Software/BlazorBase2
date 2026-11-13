using System.Globalization;
using System.Security.Claims;
using BlazorBase.Components.Services;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components.Icons.Regular;
using Microsoft.JSInterop;
using IThemeService = BlazorBase.Components.Services.IThemeService;

namespace BlazorBase.User.Components.UserMenu;

/// <summary>
/// The account menu for the header of <c>BaseLayout</c>: a 44 px button with the signed-in user's initials (or
/// avatar) that opens a menu with the user's name and account, the language and theme choice, host-provided
/// entries and "Sign out".
/// </summary>
/// <remarks>
/// Renders nothing for an anonymous visitor. The menu is a plain <c>role="menu"</c> region (no <c>FluentMenu</c>, so
/// it needs no provider and is bUnit-testable) that is only in the DOM while open: anchored below the button on
/// wide screens, a bottom sheet with a dimmed backdrop at 768 px and below. It closes on Escape, a pointer press or
/// focus outside, navigation and after an entry was chosen; focus moves to the first entry on open and back to the
/// button after Escape or a choice. A language choice is stored on the account first
/// (<see cref="IUserLanguagePreferenceSync"/>) and then applied through <see cref="ILanguageService"/>, which reloads
/// the page under WebAssembly; a theme choice goes through <see cref="IThemeService"/>, which stores it on the account
/// itself. "Sign out" runs <see cref="IUserSignOutService"/> and navigates to <see cref="LoginPath"/>.
/// </remarks>
public partial class BaseUserMenu(
    AuthenticationStateProvider authenticationStateProvider,
    ILanguageService languageService,
    IThemeService themeService,
    IUserLanguagePreferenceSync languagePreferenceSync,
    IUserSignOutService signOutService,
    NavigationManager navigationManager,
    IJSRuntime jsRuntime,
    IStringLocalizer<BaseUserMenu> localizer) : ComponentBase, IAsyncDisposable
{
    #region Injects

    private readonly AuthenticationStateProvider AuthenticationStateProvider = authenticationStateProvider;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IThemeService ThemeService = themeService;
    private readonly IUserLanguagePreferenceSync LanguagePreferenceSync = languagePreferenceSync;
    private readonly IUserSignOutService SignOutService = signOutService;
    private readonly NavigationManager NavigationManager = navigationManager;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<BaseUserMenu> Localizer = localizer;

    #endregion

    #region Parameters

    /// <summary>Shows the language choice. Default <see langword="true"/>; it is left out anyway with fewer than two <see cref="Languages"/>.</summary>
    [Parameter]
    public bool ShowLanguage { get; set; } = true;

    /// <summary>Shows the System / Light / Dark choice. Default <see langword="true"/>.</summary>
    [Parameter]
    public bool ShowTheme { get; set; } = true;

    /// <summary>
    /// The two-letter codes of the languages the app ships resources for, in display order. Defaults to
    /// <c>en</c> and <c>de</c>, the languages of the framework's own resources and the ones the account stores.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string>? Languages { get; set; }

    /// <summary>Host-provided entries (for example "Settings" or "Profile"), listed above "Sign out".</summary>
    [Parameter]
    public IReadOnlyList<UserMenuItem>? Items { get; set; }

    /// <summary>Where "Sign out" navigates to, relative to the app's base address. Default <c>login</c>.</summary>
    [Parameter]
    public string LoginPath { get; set; } = "login";

    /// <summary>An optional picture shown instead of the initials.</summary>
    [Parameter]
    public string? AvatarSrc { get; set; }

    #endregion

    #region Members

    private const string ModulePath = "./_content/BlazorBase.User/js/userMenu.js";

    private const string DisplayNameClaimType = "displayName";

    private static readonly IReadOnlyList<string> DefaultLanguages = ["en", "de"];

    private static int Counter;

    private readonly string MenuId = $"base-user-menu-{Interlocked.Increment(ref Counter)}";

    private IJSObjectReference? Module;
    private DotNetObjectReference<BaseUserMenu>? SelfReference;
    private ElementReference Root;
    private ElementReference Toggle;
    private ElementReference Popup;
    private bool IsAuthenticated;
    private bool IsOpen;
    private bool IsAttached;
    private bool IsDisposed;
    private string? PendingFocus;
    private string DisplayName = String.Empty;
    private string? AccountName;
    private string Initials = String.Empty;
    private IReadOnlyList<(string Code, string Label)> LanguageOptions = [];
    private IReadOnlyList<(ThemePreference Preference, string Label, Icon Icon)> ThemeOptions = [];

    #endregion

    private bool HasAvatarImage => !String.IsNullOrEmpty(AvatarSrc);

    private bool ShowLanguageSection => ShowLanguage && LanguageOptions.Count > 1;

    private bool ShowThemeSection => ShowTheme;

    private bool HasExtraItems => Items is { Count: > 0 };

    private string ToggleLabel => String.IsNullOrEmpty(DisplayName) ? Localizer["MenuLabel"] : Localizer["ToggleLabel", DisplayName];

    private string MenuLabel => Localizer["MenuLabel"];

    protected override async Task OnInitializedAsync()
    {
        AuthenticationStateProvider.AuthenticationStateChanged += OnAuthenticationStateChanged;
        NavigationManager.LocationChanged += OnLocationChanged;
        ThemeService.ThemeChanged += OnPreferenceChanged;
        LanguageService.LanguageChanged += OnPreferenceChanged;

        await ReadUserAsync();
    }

    protected override void OnParametersSet()
    {
        LanguageOptions = (Languages ?? DefaultLanguages)
            .Where(code => !String.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => (code, ResolveLanguageLabel(code)))
            .ToList();

        ThemeOptions =
        [
            (ThemePreference.System, Localizer["ThemeSystem"], new Size20.Desktop()),
            (ThemePreference.Light, Localizer["ThemeLight"], new Size20.WeatherSunny()),
            (ThemePreference.Dark, Localizer["ThemeDark"], new Size20.WeatherMoon()),
        ];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsDisposed)
            return;

        if (IsOpen != IsAttached)
            await SynchronizeAttachmentAsync();

        if (!IsOpen || PendingFocus is null)
            return;

        var position = PendingFocus;
        PendingFocus = null;
        var module = await LoadModuleAsync();
        await module.InvokeVoidAsync("focusItem", Popup, position);
    }

    private async Task SynchronizeAttachmentAsync()
    {
        var module = await LoadModuleAsync();
        if (IsOpen)
        {
            SelfReference ??= DotNetObjectReference.Create(this);
            await module.InvokeVoidAsync("attach", Root, SelfReference);
        }
        else
        {
            await module.InvokeVoidAsync("detach");
        }

        IsAttached = IsOpen;
    }

    /// <summary>
    /// Called from the browser when the user pressed Escape, or pressed the pointer or moved the focus outside the menu.
    /// </summary>
    [JSInvokable]
    public Task OnDismissRequested(bool restoreFocus) => InvokeAsync(() => CloseAsync(restoreFocus));

    private bool IsCurrentLanguage(string code) =>
        String.Equals(code, LanguageService.CurrentLanguage, StringComparison.OrdinalIgnoreCase);

    private Task ToggleAsync() => IsOpen ? CloseAsync(true) : OpenAsync("first");

    private Task OpenAsync(string focusPosition)
    {
        IsOpen = true;
        PendingFocus = focusPosition;
        return Task.CompletedTask;
    }

    private async Task CloseAsync(bool restoreFocus)
    {
        if (!IsOpen)
            return;

        IsOpen = false;
        PendingFocus = null;
        StateHasChanged();

        if (restoreFocus)
            await Toggle.FocusAsync();
    }

    private Task CloseFromBackdropAsync() => CloseAsync(false);

    private Task CloseAfterLinkAsync() => CloseAsync(false);

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            await CloseAsync(true);
    }

    private async Task OnToggleKeyDown(KeyboardEventArgs args)
    {
        if (IsOpen)
            return;

        if (args.Key == "ArrowDown")
            await OpenAsync("first");
        else if (args.Key == "ArrowUp")
            await OpenAsync("last");
    }

    private async Task SelectLanguageAsync(string code)
    {
        await CloseAsync(true);

        if (IsCurrentLanguage(code))
            return;

        await LanguagePreferenceSync.SaveAsync(code);
        await LanguageService.SetLanguageAsync(code);
    }

    private async Task SelectThemeAsync(ThemePreference preference)
    {
        await CloseAsync(true);

        if (ThemeService.CurrentTheme == preference)
            return;

        await ThemeService.SetThemeAsync(preference);
    }

    private async Task RunItemAsync(UserMenuItem item)
    {
        await CloseAsync(true);

        if (item.OnClick is not null)
            await item.OnClick();
    }

    private async Task SignOutAsync()
    {
        await CloseAsync(false);
        await SignOutService.SignOutAsync();
        NavigationManager.NavigateTo(LoginPath);
    }

    private async Task ReadUserAsync()
    {
        var authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authenticationState.User;

        IsAuthenticated = user.Identity?.IsAuthenticated == true;
        if (!IsAuthenticated)
        {
            IsOpen = false;
            DisplayName = String.Empty;
            AccountName = null;
            Initials = String.Empty;
            return;
        }

        var email = NonEmpty(user.FindFirst(ClaimTypes.Email)?.Value);
        var userName = NonEmpty(user.FindFirst(ClaimTypes.Name)?.Value) ?? NonEmpty(user.Identity?.Name);
        DisplayName = NonEmpty(user.FindFirst(DisplayNameClaimType)?.Value) ?? userName ?? email ?? String.Empty;

        var account = email ?? userName;
        AccountName = String.Equals(account, DisplayName, StringComparison.OrdinalIgnoreCase) ? null : account;
        Initials = BuildInitials(DisplayName);
    }

    private string ResolveLanguageLabel(string code)
    {
        var localized = Localizer[$"Language_{code}"];
        if (!localized.ResourceNotFound)
            return localized.Value;

        try
        {
            var nativeName = CultureInfo.GetCultureInfo(code).NativeName;
            return String.IsNullOrEmpty(nativeName) ? code : String.Concat(nativeName[..1].ToUpper(CultureInfo.GetCultureInfo(code)), nativeName[1..]);
        }
        catch (CultureNotFoundException)
        {
            return code;
        }
    }

    /// <summary>
    /// The first letters of the first and the last word of the name; one letter for a single word (an e-mail address
    /// counts as one), "?" without a name.
    /// </summary>
    private static string BuildInitials(string name)
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";

        var first = parts[0][..1];

        return parts.Length == 1
            ? first.ToUpperInvariant()
            : String.Concat(first, parts[^1][..1]).ToUpperInvariant();
    }

    private static string? NonEmpty(string? value) => String.IsNullOrWhiteSpace(value) ? null : value;

    private async Task<IJSObjectReference> LoadModuleAsync() =>
        Module ??= await JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);

    private void OnAuthenticationStateChanged(Task<AuthenticationState> authenticationStateTask) =>
        _ = InvokeAsync(async () =>
        {
            if (IsDisposed)
                return;

            await ReadUserAsync();
            StateHasChanged();
        });

    private void OnLocationChanged(object? sender, LocationChangedEventArgs args) =>
        _ = InvokeAsync(() => CloseAsync(false));

    private void OnPreferenceChanged() => _ = InvokeAsync(() =>
    {
        if (!IsDisposed)
            StateHasChanged();
    });

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        AuthenticationStateProvider.AuthenticationStateChanged -= OnAuthenticationStateChanged;
        NavigationManager.LocationChanged -= OnLocationChanged;
        ThemeService.ThemeChanged -= OnPreferenceChanged;
        LanguageService.LanguageChanged -= OnPreferenceChanged;

        if (Module is not null)
        {
            try
            {
                await Module.InvokeVoidAsync("detach");
                await Module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }

            Module = null;
        }

        SelfReference?.Dispose();
        SelfReference = null;
        GC.SuppressFinalize(this);
    }
}
