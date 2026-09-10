namespace BlazorBase.User.Server.Security;

/// <summary>
/// Tracks whether <c>app.UseBlazorBaseAccessTokenStampValidation()</c> actually ran, so
/// <see cref="BlazorBaseAccessTokenStampValidationStartupGuardFilter"/> can tell a host that forgot
/// the call from one that wired it correctly. Registered once per <c>AddBlazorBaseUserServer</c> call
/// that claims the authentication default; set once, from
/// <c>UseBlazorBaseAccessTokenStampValidation()</c>, before the first request is ever served.
/// </summary>
internal sealed class BlazorBaseAccessTokenStampValidationMiddlewareRegistration
{
    public bool WasRegistered { get; set; }
}
