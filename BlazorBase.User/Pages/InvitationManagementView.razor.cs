using BlazorBase.Components.Dialogs;
using BlazorBase.Components.Services;
using BlazorBase.User.Components.Invitations;
using BlazorBase.User.Models;
using BlazorBase.User.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.User.Pages;

/// <summary>
/// Generic invitation administration over <see cref="IUserInvitationService"/>: lists the invitations with
/// their status, opens <see cref="InviteUserDialog"/> to invite someone, and per open invitation resends the
/// email, creates a new copyable link or revokes it after confirmation. The newest link is shown with a copy
/// button, with a warning when no email went out. Like <see cref="UserManagementView"/> it carries no route,
/// layout or authorization; the host places it on an administrator page.
/// </summary>
/// <remarks>
/// Resending and a new link both replace the token, so the previous link stops working. The roles offered
/// come from the registered <see cref="IUserRoleProvider"/>.
/// </remarks>
public partial class InvitationManagementView(
    IUserInvitationService invitationService,
    IDialogService dialogService,
    IConfirmationService confirmationService,
    IEnumerable<IUserRoleProvider> roleProviders,
    NavigationManager navigation,
    IJSRuntime jsRuntime,
    IStringLocalizer<InvitationManagementView> localizer) : ComponentBase
{
    #region Injects
    private readonly IUserInvitationService InvitationService = invitationService;
    private readonly IDialogService DialogService = dialogService;
    private readonly IConfirmationService ConfirmationService = confirmationService;
    private readonly IUserRoleProvider? RoleProvider = roleProviders.LastOrDefault();
    private readonly NavigationManager Navigation = navigation;
    private readonly IJSRuntime JsRuntime = jsRuntime;
    private readonly IStringLocalizer<InvitationManagementView> Localizer = localizer;
    #endregion

    private const string DialogWidth = "min(480px, calc(100vw - 2rem))";

    /// <summary>
    /// Optional application fields for the invite dialog. They receive the request and set its
    /// <see cref="CreateUserInvitationRequest.Context"/>.
    /// </summary>
    [Parameter]
    public RenderFragment<CreateUserInvitationRequest>? ContextContent { get; set; }

    /// <summary>The role preselected in the invite dialog; the first offered role when empty.</summary>
    [Parameter]
    public string? DefaultRole { get; set; }

    /// <summary>The language of the invitation emails and of the new accounts; the user type's default when empty.</summary>
    [Parameter]
    public string? Language { get; set; }

    private IReadOnlyList<UserInvitationSummary> Invitations { get; set; } = [];
    private UserInvitationLinkResponse? LatestLink { get; set; }
    private string? LatestLinkMessage { get; set; }
    private string? CopyMessage { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsBusy { get; set; }

    private string AbsoluteLatestLink => LatestLink is null ? string.Empty : Navigation.ToAbsoluteUri(LatestLink.Link).ToString();

    protected override async Task OnInitializedAsync() => await LoadInvitationsAsync();

    private async Task LoadInvitationsAsync()
    {
        IsLoading = true;

        try
        {
            Invitations = await InvitationService.GetInvitationsAsync();
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

    private async Task OpenInviteDialogAsync()
    {
        var content = new InviteUserDialogContent
        {
            Roles = RoleProvider?.GetRoleNames() ?? [],
            DefaultRole = DefaultRole,
            Language = Language,
            ContextContent = ContextContent,
        };

        var result = await DialogService.ShowBaseDialogAsync<InviteUserDialog, InviteUserDialogContent>(Localizer["InviteTitle"], content, DialogWidth);

        if (result is not { Cancelled: false, Value: UserInvitationLinkResponse response })
            return;

        ShowLink(response, Localizer["Created"]);
        await LoadInvitationsAsync();
    }

    private Task ResendAsync(UserInvitationSummary invitation) =>
        RunAsync(async () => ShowLink(await InvitationService.ResendAsync(invitation.Id), Localizer["Resent"]));

    private Task RenewLinkAsync(UserInvitationSummary invitation) =>
        RunAsync(async () => ShowLink(await InvitationService.RenewLinkAsync(invitation.Id), Localizer["NewLinkCreated"]));

    private async Task RevokeAsync(UserInvitationSummary invitation)
    {
        var confirmed = await ConfirmationService.ConfirmDestructiveAsync(
            string.Format(Localizer["RevokeConfirmMessage"], invitation.Email),
            Localizer["RevokeConfirmTitle"],
            Localizer["Revoke"],
            Localizer["Cancel"]);

        if (!confirmed)
            return;

        await RunAsync(async () =>
        {
            await InvitationService.RevokeAsync(invitation.Id);
            if (LatestLink?.Invitation.Id == invitation.Id)
                LatestLink = null;
        });
    }

    private async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await action();
            await LoadInvitationsAsync();
        }
        catch (UserInvitationException exception)
        {
            ErrorMessage = exception.ErrorCode == UserInvitationErrorCodes.UserExists
                ? Localizer["UserExists"]
                : string.IsNullOrWhiteSpace(exception.Message) ? Localizer["ActionError"] : exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = Localizer["ActionError"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ShowLink(UserInvitationLinkResponse response, string actionMessage)
    {
        LatestLink = response;
        CopyMessage = null;
        LatestLinkMessage = response.EmailSent
            ? string.Format(Localizer["EmailSent"], actionMessage, response.Invitation.Email)
            : string.Format(Localizer["EmailNotSent"], actionMessage);
    }

    private async Task CopyLatestLinkAsync()
    {
        try
        {
            await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", AbsoluteLatestLink);
            CopyMessage = Localizer["Copied"];
        }
        catch (JSException)
        {
            CopyMessage = Localizer["CopyFailed"];
        }
    }

    private static bool CanChange(UserInvitationSummary invitation) =>
        invitation.Status is UserInvitationStatus.Pending or UserInvitationStatus.Expired;

    private static string StatusClass(UserInvitationStatus status) =>
        $"invitation-management-status invitation-management-status-{status.ToString().ToLowerInvariant()}";

    private string DescribeDates(UserInvitationSummary invitation) => invitation.Status switch
    {
        UserInvitationStatus.Accepted => string.Format(Localizer["AcceptedOn"], FormatDate(invitation.AcceptedAt)),
        UserInvitationStatus.Revoked => string.Format(Localizer["RevokedOn"], FormatDate(invitation.RevokedAt)),
        UserInvitationStatus.Expired => string.Format(Localizer["ExpiredOn"], FormatDate(invitation.ExpiresAt)),
        _ => string.Format(Localizer["ExpiresOn"], FormatDate(invitation.ExpiresAt)),
    };

    private static string FormatDate(DateTime? utcDate) =>
        utcDate is null ? string.Empty : DateTime.SpecifyKind(utcDate.Value, DateTimeKind.Utc).ToLocalTime().ToString("g");
}
