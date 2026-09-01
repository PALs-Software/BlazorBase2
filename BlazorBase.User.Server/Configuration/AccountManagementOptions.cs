namespace BlazorBase.User.Server.Configuration;

/// <summary>
/// Settings for the account-management endpoints (email confirmation, password reset, two-factor
/// enrolment) mapped by <c>MapBlazorBaseAccountEndpoints</c>.
/// </summary>
public class AccountManagementOptions
{
    public const string SectionName = "AccountManagement";

    /// <summary>
    /// The issuer name embedded in the authenticator app's <c>otpauth://</c> URI, shown next to the
    /// account entry it creates. Defaults to <c>BlazorBase</c>; a host should override this with its
    /// own product name.
    /// </summary>
    public string Issuer { get; set; } = "BlazorBase";

    /// <summary>
    /// Base URL of the client's own confirm-email and reset-password pages, used to build the links
    /// sent by email - this API has no server-rendered pages of its own to redirect to. Leave unset to
    /// emit a relative link, which only makes sense when the client is served from the same origin as
    /// this API.
    /// </summary>
    public string? ClientBaseUrl { get; set; }
}
