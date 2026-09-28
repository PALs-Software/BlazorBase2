namespace BlazorBase.User.Server.Tokens;

/// <summary>
/// Tracks whether <c>app.UseBlazorBaseCsrf()</c> actually ran, so
/// <see cref="BlazorBaseCsrfStartupGuardFilter"/> can tell a host that forgot the call from one that
/// wired it correctly. Registered once per <c>AddBlazorBaseCookieAuthTokens</c> call; set once, from
/// <c>UseBlazorBaseCsrf()</c>, before the first request is ever served.
/// </summary>
internal sealed class BlazorBaseCsrfMiddlewareRegistration
{
    public bool WasRegistered { get; set; }
}
