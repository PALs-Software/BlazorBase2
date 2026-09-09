namespace BlazorBase.User.SecurityHarness.Hosting;

/// <summary>
/// The set of host configurations the harness attacks, one Kestrel instance per value so a
/// misconfiguration (missing CSRF middleware, development login left on in production) is a
/// first-class, separately reported case rather than a variant of a single host.
/// </summary>
public enum HarnessProfile
{
    /// <summary><c>AddBlazorBaseUserServer</c> with defaults, over HTTP.</summary>
    Bearer,

    /// <summary>The same registration as <see cref="Bearer"/>, over HTTPS with a self-signed loopback certificate.</summary>
    BearerHttps,

    /// <summary><c>AddBlazorBaseCookieAuthTokens</c> plus <c>app.UseBlazorBaseCsrf()</c>, over HTTPS.</summary>
    Cookie,

    /// <summary>The cookie transport with <c>app.UseBlazorBaseCsrf()</c> deliberately not called.</summary>
    CookieWithoutCsrfMiddleware,

    /// <summary><c>AddBlazorBaseExternalAuth</c> pointed at the fake identity provider added in step 2.</summary>
    External,

    /// <summary><c>AddBlazorBaseAccessTokens</c> with defaults.</summary>
    AccessTokens,

    /// <summary><c>AddBlazorBaseDevelopmentAuthentication</c> in the Development environment.</summary>
    DevelopmentLogin,

    /// <summary>The same registration as <see cref="DevelopmentLogin"/>, with <c>EnvironmentName = "Production"</c>.</summary>
    ProductionWithDevelopmentLogin,
}

/// <summary>Human-readable description of what each <see cref="HarnessProfile"/> wires up, for the report.</summary>
public static class HarnessProfileExtensions
{
    public static string Describe(this HarnessProfile profile) => profile switch
    {
        HarnessProfile.Bearer => "AddBlazorBaseUserServer with defaults, over HTTP.",
        HarnessProfile.BearerHttps => "AddBlazorBaseUserServer with defaults, over HTTPS with a self-signed loopback certificate.",
        HarnessProfile.Cookie => "AddBlazorBaseCookieAuthTokens plus app.UseBlazorBaseCsrf(), over HTTPS.",
        HarnessProfile.CookieWithoutCsrfMiddleware => "Cookie transport, over HTTPS, with app.UseBlazorBaseCsrf() deliberately not called.",
        HarnessProfile.External => "AddBlazorBaseExternalAuth pointed at the fake identity provider added in step 2.",
        HarnessProfile.AccessTokens => "AddBlazorBaseAccessTokens with defaults.",
        HarnessProfile.DevelopmentLogin => "AddBlazorBaseDevelopmentAuthentication in the Development environment.",
        HarnessProfile.ProductionWithDevelopmentLogin => "AddBlazorBaseDevelopmentAuthentication with EnvironmentName = \"Production\".",
        _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, null),
    };
}
