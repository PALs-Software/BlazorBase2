namespace BlazorBase.User.Services;

/// <summary>
/// Signs the current user out under either authentication transport: ends the session on the server and drops
/// the client to an anonymous state. Navigating to the login page afterwards is the caller's decision.
/// </summary>
public interface IUserSignOutService
{
    /// <summary>
    /// Signs the user out. The returned <see cref="SignOutResult"/> says whether the client is anonymous now
    /// (<see cref="SignOutResult.SignedOut"/>, <see cref="SignOutResult.SignedOutWithoutServerRevocation"/>) or
    /// still signed in because the server did not confirm the logout (<see cref="SignOutResult.Failed"/>).
    /// </summary>
    Task<SignOutResult> SignOutAsync();
}
