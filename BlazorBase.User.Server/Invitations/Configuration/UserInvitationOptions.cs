namespace BlazorBase.User.Server.Invitations.Configuration;

/// <summary>
/// Settings of the invitation flow, bound from the <see cref="SectionName"/> configuration section by
/// <c>AddBlazorBaseUserInvitations</c>. The accept link is built from
/// <c>AccountManagement:ClientBaseUrl</c> plus <see cref="AcceptPagePath"/>; the brand of the
/// invitation email comes from <c>EmailBranding</c> (<c>BlazorBase.Mailing</c>).
/// </summary>
public class UserInvitationOptions
{
    public const string SectionName = "Invitations";

    /// <summary>How long a link stays usable after it was created, resent or renewed. At least 1.</summary>
    public int LifetimeDays { get; set; } = 7;

    /// <summary>
    /// The client page that redeems a link, relative to <c>AccountManagement:ClientBaseUrl</c>. The
    /// default matches the accept page of <c>BlazorBase.User</c>.
    /// </summary>
    public string AcceptPagePath { get; set; } = "invitation/accept";
}
