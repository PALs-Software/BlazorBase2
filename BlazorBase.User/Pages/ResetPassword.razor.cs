using BlazorBase.Components.Routing;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// The anonymous <c>/reset-password?email=...&amp;token=...</c> page the password reset email links to (the
/// link <c>MapBlazorBaseAccountEndpoints</c> builds). It asks for the new password twice and submits it with
/// the token through <see cref="IAuthService.ResetPasswordAsync"/>; a refused token or password shows the
/// server's reason.
/// </summary>
public partial class ResetPassword(IAuthService authService, NavigationManager navigation, IStringLocalizer<ResetPassword> localizer)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    private readonly NavigationManager Navigation = navigation;
    private readonly IStringLocalizer<ResetPassword> Localizer = localizer;
    #endregion

    private const string EmailParameterName = "email";
    private const string TokenParameterName = "token";

    private ResetPasswordRequest RequestModel { get; set; } = new();
    private EditContext? RequestEditContext { get; set; }
    private string PasswordConfirmation { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private bool HasLinkParameters { get; set; }
    private bool IsSubmitting { get; set; }
    private bool IsCompleted { get; set; }

    protected override void OnInitialized()
    {
        RequestModel.Email = QueryStringReader.TryReadValue(Navigation.Uri, EmailParameterName) ?? string.Empty;
        RequestModel.Token = QueryStringReader.TryReadValue(Navigation.Uri, TokenParameterName) ?? string.Empty;
        HasLinkParameters = !string.IsNullOrWhiteSpace(RequestModel.Email) && !string.IsNullOrWhiteSpace(RequestModel.Token);
        RequestEditContext = new EditContext(RequestModel);
    }

    private async Task SubmitAsync()
    {
        ErrorMessage = null;

        if (RequestModel.NewPassword != PasswordConfirmation)
        {
            ErrorMessage = Localizer["PasswordsDoNotMatch"];
            return;
        }

        IsSubmitting = true;

        try
        {
            await AuthService.ResetPasswordAsync(RequestModel);
            IsCompleted = true;
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(exception.Message) || exception.StatusCode is null
                ? Localizer["SubmitError"]
                : exception.Message;
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}
