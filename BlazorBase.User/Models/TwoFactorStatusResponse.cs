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
}
