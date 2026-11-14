using BlazorBase.User.Models;

namespace BlazorBase.User.Services;

/// <summary>
/// Client for the invitation API mapped by <c>MapBlazorBaseUserInvitationEndpoints</c>. The administration
/// members are called as the signed-in administrator; <see cref="GetPreviewAsync"/> and
/// <see cref="AcceptAsync"/> are anonymous. Register it with <c>AddBlazorBaseUserInvitationClient</c> and add
/// the handlers of the application's transport to the returned builder.
/// </summary>
/// <remarks>
/// A refusal of the server surfaces as <see cref="UserInvitationException"/> carrying the
/// <see cref="UserInvitationErrorCodes"/> value; a network failure as <see cref="HttpRequestException"/>.
/// </remarks>
public interface IUserInvitationService
{
    /// <summary>Returns the invitations the administrator may manage, newest first.</summary>
    Task<IReadOnlyList<UserInvitationSummary>> GetInvitationsAsync();

    /// <summary>Creates an invitation; the server sends the email when mailing is configured.</summary>
    Task<UserInvitationLinkResponse> CreateAsync(CreateUserInvitationRequest request);

    /// <summary>Creates a new link for the invitation and sends the email again; the old link stops working.</summary>
    Task<UserInvitationLinkResponse> ResendAsync(Guid invitationId);

    /// <summary>Creates a new link for the invitation without an email; the old link stops working.</summary>
    Task<UserInvitationLinkResponse> RenewLinkAsync(Guid invitationId);

    /// <summary>Withdraws an invitation that was not accepted yet.</summary>
    Task RevokeAsync(Guid invitationId);

    /// <summary>What the accept page may show for <paramref name="token"/>; never throws for an unusable token.</summary>
    Task<UserInvitationPreview> GetPreviewAsync(string token);

    /// <summary>Redeems the token and returns the session of the new account, as a login does.</summary>
    Task<LoginResponse> AcceptAsync(AcceptUserInvitationRequest request);
}
