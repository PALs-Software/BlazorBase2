using Microsoft.Extensions.Localization;
using BlazorBase.User.Models;
using BlazorBase.User.Services;

namespace BlazorBase.User.Pages;

/// <summary>
/// Lets the signed-in account enrol in, or unenrol from, TOTP two-factor authentication: shows the
/// <c>otpauth://</c> authenticator URI to scan while enrolling, confirms enrolment with a code, and
/// displays a freshly generated batch of recovery codes exactly once - the server keeps only their
/// hashes afterward, so this is the only chance to record them.
/// </summary>
public partial class TwoFactorSettings(IAuthService authService, IStringLocalizer<TwoFactorSettings> localizer)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    private readonly IStringLocalizer<TwoFactorSettings> Localizer = localizer;
    #endregion

    private TwoFactorStatusResponse? Status { get; set; }
    private string EnableCode { get; set; } = string.Empty;
    private List<string>? RecoveryCodes { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsSubmitting { get; set; }

    protected override async Task OnInitializedAsync() => await ReloadStatusAsync();

    private async Task ReloadStatusAsync()
    {
        IsLoading = true;

        try
        {
            Status = await AuthService.GetTwoFactorStatusAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["LoadError"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task HandleEnable()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var result = await AuthService.EnableTwoFactorAsync(new TwoFactorEnableRequest { Code = EnableCode });
            RecoveryCodes = result.RecoveryCodes;
            EnableCode = string.Empty;
            await ReloadStatusAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["InvalidCode"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task HandleDisable()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            await AuthService.DisableTwoFactorAsync();
            RecoveryCodes = null;
            await ReloadStatusAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["DisableError"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private async Task HandleRegenerateRecoveryCodes()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            var result = await AuthService.RegenerateRecoveryCodesAsync();
            RecoveryCodes = result.RecoveryCodes;
            await ReloadStatusAsync();
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["RegenerateError"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
