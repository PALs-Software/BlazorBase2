namespace BlazorBase.User.Models;

/// <summary>
/// The calling account's two-factor state. <see cref="AuthenticatorUri"/> is only populated while
/// two-factor is not yet enabled - it is the <c>otpauth://</c> payload a client renders as a QR code
/// for the authenticator app to scan before confirming enrolment through <c>2fa/enable</c>.
/// </summary>
public class TwoFactorStatusResponse
{
    public bool Enabled { get; set; }

    public int RecoveryCodesRemaining { get; set; }

    public string? AuthenticatorUri { get; set; }

    /// <summary>
    /// Populated the first time this call generates the account's authenticator key, since that
    /// rotates the security stamp and would otherwise invalidate the caller's own access and refresh
    /// tokens. A client must adopt it the same way it adopts the pair from <c>login</c>; a client that
    /// ignores it keeps using its now-stale tokens and is signed out on its next request.
    /// </summary>
    public LoginResponse? RefreshedTokens { get; set; }
}
