namespace BlazorBase.User.Models;

/// <summary>
/// A freshly generated batch of recovery codes. Shown to the caller exactly once - the server keeps
/// only their hashes, the same way it keeps no plaintext password.
/// </summary>
public class TwoFactorRecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = [];

    /// <summary>
    /// Populated when enabling two-factor rotated the security stamp - see
    /// <see cref="TwoFactorStatusResponse.RefreshedTokens"/> for why a client must adopt it.
    /// <see langword="null"/> when this batch came from regenerating recovery codes instead, which
    /// does not rotate the stamp.
    /// </summary>
    public LoginResponse? RefreshedTokens { get; set; }
}
