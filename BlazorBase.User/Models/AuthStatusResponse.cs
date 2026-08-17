namespace BlazorBase.User.Models;

public class AuthStatusResponse
{
    public bool HasUsers { get; set; }

    /// <summary>
    /// True when the server offers <c>api/auth/development-login</c>, which only happens in the
    /// Development environment with the option switched on. The client uses it to sign itself in
    /// without a login form; it is always false in any other environment.
    /// </summary>
    public bool DevelopmentAuthenticationEnabled { get; set; }
}
