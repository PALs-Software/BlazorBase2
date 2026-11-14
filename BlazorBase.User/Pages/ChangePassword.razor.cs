using System.Net;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// The authenticated <c>/account/password</c> page: the signed-in user enters the current password and
/// the new one twice and submits them through <see cref="IAuthService.ChangePasswordAsync"/>. A missing
/// field and a confirmation that does not match are reported next to the field without a request; the
/// server's refusal is mapped back to the fields (wrong current password, the reasons a new password
/// is rejected) or, when it belongs to no field, shown above the button. On success the form is replaced
/// by a confirmation and the user stays signed in.
/// </summary>
public partial class ChangePassword(IAuthService authService, IStringLocalizer<ChangePassword> localizer)
{
    #region Injects
    private readonly IAuthService AuthService = authService;
    private readonly IStringLocalizer<ChangePassword> Localizer = localizer;
    #endregion

    private const string CurrentPasswordField = "currentPassword";
    private const string NewPasswordField = "newPassword";

    private ChangePasswordRequest RequestModel { get; set; } = new();
    private string PasswordConfirmation { get; set; } = string.Empty;
    private bool ShowCurrentPassword { get; set; }
    private bool ShowNewPassword { get; set; }
    private bool ShowConfirmation { get; set; }
    private string? CurrentPasswordError { get; set; }
    private IReadOnlyList<string> NewPasswordErrors { get; set; } = [];
    private string? ErrorMessage { get; set; }
    private bool HasSubmitAttempt { get; set; }
    private bool IsSubmitting { get; set; }
    private bool IsCompleted { get; set; }

    private string? ConfirmationError => ConfirmationMismatches
        ? Localizer["PasswordsDoNotMatch"]
        : null;

    private bool ConfirmationMismatches =>
        (HasSubmitAttempt || !string.IsNullOrEmpty(PasswordConfirmation)) && PasswordConfirmation != RequestModel.NewPassword;

    private string VisibilityLabel(bool isVisible) => isVisible ? Localizer["HidePassword"] : Localizer["ShowPassword"];

    private void ToggleCurrentPassword() => ShowCurrentPassword = !ShowCurrentPassword;

    private void ToggleNewPassword() => ShowNewPassword = !ShowNewPassword;

    private void ToggleConfirmation() => ShowConfirmation = !ShowConfirmation;

    private async Task SubmitAsync()
    {
        ClearServerFeedback();
        HasSubmitAttempt = true;

        if (!ValidateLocally())
            return;

        IsSubmitting = true;

        try
        {
            var outcome = await AuthService.ChangePasswordAsync(RequestModel);

            if (outcome.Succeeded)
            {
                RequestModel = new ChangePasswordRequest();
                PasswordConfirmation = string.Empty;
                IsCompleted = true;
                return;
            }

            ApplyRefusal(outcome);
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = exception.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Localizer["SessionExpired"],
                HttpStatusCode.TooManyRequests => Localizer["TooManyAttempts"],
                _ => Localizer["SubmitError"],
            };
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private bool ValidateLocally()
    {
        if (string.IsNullOrEmpty(RequestModel.CurrentPassword))
            CurrentPasswordError = Localizer["CurrentPasswordRequired"];

        if (string.IsNullOrEmpty(RequestModel.NewPassword))
            NewPasswordErrors = [Localizer["NewPasswordRequired"]];

        return CurrentPasswordError is null && NewPasswordErrors.Count == 0 && !ConfirmationMismatches;
    }

    private void ApplyRefusal(ChangePasswordOutcome outcome)
    {
        if (outcome.Code == ChangePasswordErrorCodes.CurrentPasswordIncorrect)
        {
            CurrentPasswordError = Localizer["CurrentPasswordIncorrect"];
            return;
        }

        if (outcome.Code == ChangePasswordErrorCodes.NoPasswordSet)
        {
            ErrorMessage = Localizer["NoPasswordSet"];
            return;
        }

        if (outcome.FieldErrors.TryGetValue(CurrentPasswordField, out var currentPasswordReasons) && currentPasswordReasons.Length > 0)
            CurrentPasswordError = string.Join(" ", currentPasswordReasons);

        if (outcome.FieldErrors.TryGetValue(NewPasswordField, out var newPasswordReasons) && newPasswordReasons.Length > 0)
            NewPasswordErrors = newPasswordReasons;

        if (CurrentPasswordError is null && NewPasswordErrors.Count == 0)
            ErrorMessage = Localizer["SubmitError"];
    }

    private void ClearServerFeedback()
    {
        CurrentPasswordError = null;
        NewPasswordErrors = [];
        ErrorMessage = null;
    }
}
