using BlazorBase.Components.Routing;
using BlazorBase.Components.Services;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Pages;

/// <summary>
/// The anonymous page an invitation email links to (<c>/invitation/accept?token=...</c>). It shows the
/// invited email address, asks for a display name and a password, redeems the token and signs the new
/// account in the way the login page does. An unusable link shows one message whatever the reason, because
/// the server answers every unusable token identically.
/// </summary>
public partial class AcceptInvitation(
    IUserInvitationService invitationService,
    BlazorBaseUserAuthStateProvider authStateProvider,
    NavigationManager navigation,
    ILanguageService languageService,
    IStringLocalizer<AcceptInvitation> localizer)
{
    #region Injects
    private readonly IUserInvitationService InvitationService = invitationService;
    private readonly BlazorBaseUserAuthStateProvider AuthStateProvider = authStateProvider;
    private readonly NavigationManager Navigation = navigation;
    private readonly ILanguageService LanguageService = languageService;
    private readonly IStringLocalizer<AcceptInvitation> Localizer = localizer;
    #endregion

    private const string TokenParameterName = "token";

    private UserInvitationPreview? Preview { get; set; }
    private AcceptUserInvitationRequest AcceptModel { get; set; } = new();
    private EditContext? AcceptEditContext { get; set; }
    private string PasswordConfirmation { get; set; } = string.Empty;
    private string? StatusMessage { get; set; }
    private List<string> ErrorMessages { get; set; } = [];
    private bool IsLoading { get; set; } = true;
    private bool IsSubmitting { get; set; }

    protected override async Task OnInitializedAsync()
    {
        AcceptEditContext = new EditContext(AcceptModel);
        var token = QueryStringReader.TryReadValue(Navigation.Uri, TokenParameterName);

        if (string.IsNullOrWhiteSpace(token))
        {
            StatusMessage = Localizer["InvalidLink"];
            IsLoading = false;
            return;
        }

        AcceptModel.Token = token;

        try
        {
            Preview = await InvitationService.GetPreviewAsync(token);
            AcceptModel.DisplayName = Preview.DisplayName ?? string.Empty;

            if (!Preview.IsValid)
                StatusMessage = Localizer["InvalidLink"];
        }
        catch (HttpRequestException)
        {
            StatusMessage = Localizer["LoadError"];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task AcceptAsync()
    {
        ErrorMessages = [];

        if (AcceptModel.Password != PasswordConfirmation)
        {
            ErrorMessages = [Localizer["PasswordsDoNotMatch"]];
            return;
        }

        IsSubmitting = true;

        try
        {
            var response = await InvitationService.AcceptAsync(AcceptModel);
            await AuthStateProvider.MarkUserAsAuthenticated(response);
            Navigation.NavigateTo("/", forceLoad: await SignedInAccountUsesAnotherLanguageAsync());
        }
        catch (UserInvitationException exception)
        {
            HandleRefusal(exception);
        }
        catch (HttpRequestException)
        {
            ErrorMessages = [Localizer["SubmitError"]];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private void HandleRefusal(UserInvitationException exception)
    {
        switch (exception.ErrorCode)
        {
            case UserInvitationErrorCodes.InvalidToken:
                Preview = null;
                StatusMessage = Localizer["InvalidLink"];
                return;
            case UserInvitationErrorCodes.UserExists:
                Preview = null;
                StatusMessage = Localizer["AccountExists"];
                return;
            case UserInvitationErrorCodes.PasswordRejected:
            case UserInvitationErrorCodes.ValidationFailed:
                ErrorMessages = exception.Errors.Count > 0 ? [.. exception.Errors] : [Localizer["SubmitError"]];
                return;
            default:
                ErrorMessages = [string.IsNullOrWhiteSpace(exception.Message) ? Localizer["SubmitError"] : exception.Message];
                return;
        }
    }

    /// <summary>
    /// Whether the new account starts in another language than the page was rendered in, in which case the
    /// application has to be reached through a full page load - see the login page for why.
    /// </summary>
    private async Task<bool> SignedInAccountUsesAnotherLanguageAsync()
    {
        var authenticationState = await AuthStateProvider.GetAuthenticationStateAsync();
        var languageClaim = authenticationState.User.FindFirst("language")?.Value;

        return !string.IsNullOrWhiteSpace(languageClaim) && languageClaim != LanguageService.CurrentLanguage;
    }
}
