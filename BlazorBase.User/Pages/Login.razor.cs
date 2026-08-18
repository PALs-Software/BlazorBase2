using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using BlazorBase.Components.Services;

namespace BlazorBase.User.Pages;

public partial class Login(IAuthService authService, BlazorBaseUserAuthStateProvider authStateProvider, NavigationManager navigation, ILanguageService languageService, IStringLocalizer<Login> localizer, IOptions<BlazorBaseUserClientOptions> clientOptions)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;
    private readonly NavigationManager Navigation = navigation;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IStringLocalizer<Login> Localizer = localizer;
    private readonly IOptions<BlazorBaseUserClientOptions> ClientOptions = clientOptions;
    #endregion

    [Parameter]
    public bool CollectHouseholdName { get; set; }

    private LoginRequest LoginModel { get; set; } = new();
    private SetupRequest SetupModel { get; set; } = new();
    private EditContext? LoginEditContext { get; set; }
    private EditContext? SetupEditContext { get; set; }
    private string SetupPasswordConfirm { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsSetupMode { get; set; }
    private bool IsSubmitting { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (!CollectHouseholdName)
            CollectHouseholdName = ClientOptions.Value.CollectHouseholdNameOnSetup;

        LoginEditContext = new EditContext(LoginModel);
        SetupEditContext = new EditContext(SetupModel);

        try
        {
            var status = await AuthService.GetStatusAsync();
            IsSetupMode = !status.HasUsers;
        }
        catch
        {
            ErrorMessage = Localizer["ConnectionError"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HandleLoginKeyDown(KeyboardEventArgs args)
    {
        if (args.Key != "Enter")
            return;

        if (LoginEditContext is null || !LoginEditContext.Validate())
            return;

        await HandleLogin();
    }

    private async Task HandleSetupKeyDown(KeyboardEventArgs args)
    {
        if (args.Key != "Enter")
            return;

        if (SetupEditContext is null || !SetupEditContext.Validate())
            return;

        await HandleSetup();
    }

    private async Task HandleLogin()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var response = await AuthService.LoginAsync(LoginModel);
            await AuthStateProvider.MarkUserAsAuthenticated(response);
            Navigation.NavigateTo(ResolveReturnUrl(), forceLoad: await SignedInAccountUsesAnotherLanguageAsync());
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["InvalidCredentials"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task HandleSetup()
    {
        ErrorMessage = null;

        if (SetupModel.Password != SetupPasswordConfirm)
        {
            ErrorMessage = Localizer["PasswordsDoNotMatch"];
            return;
        }

        IsSubmitting = true;

        try
        {
            var response = await AuthService.SetupAsync(SetupModel);
            await AuthStateProvider.MarkUserAsAuthenticated(response);
            Navigation.NavigateTo("/", forceLoad: await SignedInAccountUsesAnotherLanguageAsync());
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = !string.IsNullOrEmpty(ex.Message) ? ex.Message : Localizer["SetupFailed"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    /// <summary>
    /// Whether the account that just signed in is configured for a different language than the one
    /// the app started in, in which case the destination has to be reached through a full page load.
    /// </summary>
    /// <remarks>
    /// Under WebAssembly the satellite resource assemblies are downloaded while the host starts, so
    /// a client-side navigation would land the account on a page still rendered from the previous
    /// language's resources.
    /// </remarks>
    private async Task<bool> SignedInAccountUsesAnotherLanguageAsync()
    {
        var authenticationState = await AuthStateProvider.GetAuthenticationStateAsync();
        var languageClaim = authenticationState.User.FindFirst("language")?.Value;

        return !string.IsNullOrWhiteSpace(languageClaim) && languageClaim != LanguageService.CurrentLanguage;
    }

    private string ResolveReturnUrl()
    {
        var rawQuery = new Uri(Navigation.Uri).Query;
        if (string.IsNullOrEmpty(rawQuery))
            return "/";

        foreach (var segment in rawQuery.TrimStart('?').Split('&'))
        {
            var separatorIndex = segment.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = Uri.UnescapeDataString(segment[..separatorIndex]);
            if (!key.Equals("returnUrl", StringComparison.OrdinalIgnoreCase))
                continue;

            var value = Uri.UnescapeDataString(segment[(separatorIndex + 1)..]);
            if (string.IsNullOrWhiteSpace(value))
                return "/";

            if (!IsSafeLocalReturnUrl(value))
                return "/";

            return value;
        }

        return "/";
    }

    private static bool IsSafeLocalReturnUrl(string value)
    {
        if (value.Length < 2 || value[0] != '/')
            return false;

        if (value[1] == '/' || value[1] == '\\')
            return false;

        return true;
    }
}
