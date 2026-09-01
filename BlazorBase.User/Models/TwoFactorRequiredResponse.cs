namespace BlazorBase.User.Models;

/// <summary>
/// Answers a password login for a two-factor-enabled account. Carries only the opaque pending token
/// needed to complete <c>login/2fa</c> - no user id, no email, no partial access token - so a caller
/// that never reaches the second step learns nothing about the account beyond "the password was
/// correct".
/// </summary>
public class TwoFactorRequiredResponse
{
    public string PendingToken { get; set; } = string.Empty;
}
