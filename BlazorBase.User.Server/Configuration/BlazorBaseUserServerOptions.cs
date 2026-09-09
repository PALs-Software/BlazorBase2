using Microsoft.AspNetCore.Identity;

namespace BlazorBase.User.Server.Configuration;

/// <summary>
/// Controls how <c>AddBlazorBaseUserServer</c> registers its bearer-token scheme, for a host that
/// already owns an authentication pipeline of its own (a policy scheme, a second bearer scheme, or
/// both) and must not have it silently overridden.
/// </summary>
/// <remarks>
/// Without this seam, <c>AddBlazorBaseUserServer</c> always registers under the fixed name
/// <see cref="IdentityConstants.BearerScheme"/> and always claims the authentication default. A host
/// that already has its own bearer scheme registered under that same name would silently lose one of
/// the two registrations - both configure delegates run, and whichever is registered last wins, with
/// no error from either. The defaults below reproduce that original, unconditional behavior exactly,
/// so existing hosts and the template are unaffected.
/// </remarks>
public class BlazorBaseUserServerOptions
{
    /// <summary>
    /// The authentication scheme name the framework's bearer-token handler registers under.
    /// Defaults to <see cref="IdentityConstants.BearerScheme"/>.
    /// </summary>
    public string SchemeName { get; set; } = IdentityConstants.BearerScheme;

    /// <summary>
    /// Whether registration sets <c>DefaultAuthenticateScheme</c> and <c>DefaultChallengeScheme</c>
    /// to <see cref="SchemeName"/>. Defaults to <see langword="true"/>. A host that already selects
    /// its own default scheme (for example a policy scheme dispatching to several handlers) sets
    /// this to <see langword="false"/> and leaves that choice untouched.
    /// </summary>
    public bool ClaimDefaultScheme { get; set; } = true;

    /// <summary>
    /// Whether this host must map <c>app.MapBlazorBaseAuthSessionEndpoint()</c>. Defaults to
    /// <see langword="true"/>: Identity's bearer access token is an opaque data-protected blob, so a
    /// BlazorBase client cannot decode its own claims under either token transport and has to ask the
    /// server for them. Set to <see langword="false"/> only when the host states, deliberately, that no
    /// BlazorBase client consumes this API - a pure Web API serving native or service clients over
    /// bearer tokens is the legitimate case. The cookie transport requires the endpoint regardless of
    /// this flag - see <see cref="BlazorBase.User.Server.Tokens.BlazorBaseAuthTokenTransportExtensions.AddBlazorBaseCookieAuthTokens"/>.
    /// </summary>
    public bool RequiresAuthSessionEndpoint { get; set; } = true;
}
