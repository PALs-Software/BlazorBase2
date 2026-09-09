namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Tracks whether <c>app.MapBlazorBaseAuthSessionEndpoint()</c> actually ran, so
/// <see cref="BlazorBaseAuthSessionEndpointStartupGuardFilter"/> can tell a host that forgot the call
/// from one that wired it correctly. Registered once per <c>AddBlazorBaseCookieAuthTokens</c> call;
/// set once, from <c>MapBlazorBaseAuthSessionEndpoint()</c>, before the first request is ever served.
/// </summary>
internal sealed class BlazorBaseAuthSessionEndpointRegistration
{
    public bool WasRegistered { get; set; }
}
