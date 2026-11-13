namespace BlazorBase.User.Services;

/// <summary>
/// Signs the current user out under either authentication transport: ends the session on the server and drops
/// the client to an anonymous state. Navigating to the login page afterwards is the caller's decision.
/// </summary>
public interface IUserSignOutService
{
    Task SignOutAsync();
}
