namespace BlazorBase.User.Models;

/// <summary>
/// A freshly generated batch of recovery codes. Shown to the caller exactly once - the server keeps
/// only their hashes, the same way it keeps no plaintext password.
/// </summary>
public class TwoFactorRecoveryCodesResponse
{
    public List<string> RecoveryCodes { get; set; } = [];
}
