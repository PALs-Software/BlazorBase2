using System.ComponentModel.DataAnnotations;

namespace BlazorBase.User.Models;

public class TwoFactorLoginRequest
{
    [Required]
    public string PendingToken { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Whether <see cref="Code"/> is one of the account's recovery codes rather than a live
    /// authenticator code - the two are verified through different <c>UserManager</c> calls.
    /// </summary>
    public bool IsRecoveryCode { get; set; }
}
