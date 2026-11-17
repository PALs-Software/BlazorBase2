using BlazorBase.Components.Dialogs;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;

namespace BlazorBase.User.Components.Invitations;

/// <summary>
/// The invite form of <see cref="Pages.InvitationManagementView"/>: email, display name, role and the
/// application's optional context fields. It creates the invitation itself, so a refusal (an existing
/// account, a handler's veto) is shown in the dialog, and closes with the
/// <see cref="UserInvitationLinkResponse"/> as result.
/// </summary>
public partial class InviteUserDialog(IUserInvitationService invitationService, IStringLocalizer<InviteUserDialog> localizer) : BaseDialogInstance<InviteUserDialogContent>
{
    #region Injects
    private readonly IUserInvitationService InvitationService = invitationService;
    private readonly IStringLocalizer<InviteUserDialog> DialogLocalizer = localizer;
    #endregion

    private CreateUserInvitationRequest Request { get; set; } = new();
    private EditContext? RequestEditContext { get; set; }
    private List<string> ErrorMessages { get; set; } = [];
    private bool IsSubmitting { get; set; }

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Request.Role = Content.DefaultRole ?? Content.Roles.FirstOrDefault() ?? string.Empty;
        Request.Language = Content.Language;
        RequestEditContext = new EditContext(Request);
    }

    private async Task SubmitAsync()
    {
        ErrorMessages = [];

        if (RequestEditContext is null || !RequestEditContext.Validate())
            return;

        IsSubmitting = true;

        try
        {
            var response = await InvitationService.CreateAsync(Request);
            await ConfirmWithResultAsync(response);
        }
        catch (UserInvitationException exception)
        {
            ErrorMessages = DescribeRefusal(exception);
        }
        catch (HttpRequestException)
        {
            ErrorMessages = [DialogLocalizer["SubmitError"]];
        }
        finally
        {
            IsSubmitting = false;
        }
    }

    private List<string> DescribeRefusal(UserInvitationException exception) => exception.ErrorCode switch
    {
        UserInvitationErrorCodes.UserExists => [DialogLocalizer["UserExists"]],
        UserInvitationErrorCodes.UnknownRole => [DialogLocalizer["UnknownRole"]],
        UserInvitationErrorCodes.ValidationFailed when exception.Errors.Count > 0 => [.. exception.Errors],
        _ => [string.IsNullOrWhiteSpace(exception.Message) ? DialogLocalizer["SubmitError"] : exception.Message],
    };
}
