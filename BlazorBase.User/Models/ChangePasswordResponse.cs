namespace BlazorBase.User.Models;

/// <summary>
/// Answers a successful <c>change-password</c>. Changing the password rotates the account's security
/// stamp, which ends every other session and refresh token of the account; <see cref="RefreshedTokens"/>
/// carries the replacement for the caller's own session.
/// </summary>
public class ChangePasswordResponse
{
    /// <summary>
    /// The caller's replacement token pair. A client under the bearer transport must adopt it the same
    /// way it adopts the pair from <c>login</c>; a client that ignores it keeps using its now-stale
    /// tokens and is signed out on its next request. Under the cookie transport the session cookie has
    /// already been rewritten and both token strings are empty.
    /// </summary>
    public LoginResponse? RefreshedTokens { get; set; }
}
