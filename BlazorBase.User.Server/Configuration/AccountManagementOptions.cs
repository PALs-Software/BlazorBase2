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

    /// <summary>
    /// Lower bound, in milliseconds, of the randomised response-time floor <c>forgot-password</c> and
    /// <c>resend-confirmation</c> pad up to, so that looking up the user and generating a token on the
    /// existing-address path does not leak through response latency what the identical response body
    /// already hides. Set both this and <see cref="TimingFloorMaximumMilliseconds"/> to <c>0</c> to
    /// disable the padding entirely, which is what the test host does. With the default background
    /// mail dispatch, the send itself is off the response path and does not need to be covered by this
    /// floor; a host that switches to <see cref="BlazorBase.Mailing.Models.EmailDispatchMode.Inline"/>
    /// puts the send back on the response path and must raise this value to cover its own mail-send
    /// latency again.
    /// </summary>
    public int TimingFloorMinimumMilliseconds { get; set; } = 750;

    /// <summary>
    /// Upper bound, in milliseconds, of the randomised response-time floor - see
    /// <see cref="TimingFloorMinimumMilliseconds"/>. A value below the minimum is a misconfiguration;
    /// the endpoint clamps it up to the minimum rather than producing a negative delay.
    /// </summary>
    public int TimingFloorMaximumMilliseconds { get; set; } = 1250;
}
