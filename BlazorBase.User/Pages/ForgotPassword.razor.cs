using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// The anonymous <c>/forgot-password</c> page: asks for the email address and requests a reset link through
/// <see cref="IAuthService.ForgotPasswordAsync"/>. It confirms in the same words whether or not the address
/// belongs to an account, matching the server, which answers both cases identically.
/// </summary>
public partial class ForgotPassword(IAuthService authService, IStringLocalizer<ForgotPassword> localizer)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    private readonly IStringLocalizer<ForgotPassword> Localizer = localizer;
    #endregion

    private ForgotPasswordRequest RequestModel { get; set; } = new();
    private EditContext? RequestEditContext { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsSubmitting { get; set; }
    private bool IsSent { get; set; }

    protected override void OnInitialized() => RequestEditContext = new EditContext(RequestModel);

    private async Task SubmitAsync()
    {
        ErrorMessage = null;
        IsSubmitting = true;

        try
        {
            await AuthService.ForgotPasswordAsync(RequestModel);
            IsSent = true;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["SubmitError"];
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
