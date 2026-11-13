namespace BlazorBase.User.Services;

/// <summary>The outcome of <see cref="IUserSignOutService.SignOutAsync"/>.</summary>
public enum SignOutResult
{
    /// <summary>
    /// The server confirmed the logout and the client is anonymous now. The caller navigates to the login page.
    /// </summary>
    SignedOut,

    /// <summary>
    /// Bearer transport only: the client dropped its tokens and is anonymous now, but the server did not confirm
    /// the revocation, so the refresh token family may stay valid until it expires. The caller still navigates to
    /// the login page and may tell the user.
    /// </summary>
    SignedOutWithoutServerRevocation,

    /// <summary>
    /// Cookie transport only: the server did not confirm the logout (network error, timeout, antiforgery rejection
    /// or any other non-success status), so its session and cookie are still alive. The client stays signed in and
    /// nothing was changed locally; the caller tells the user and offers a retry.
    /// </summary>
    Failed,
}
