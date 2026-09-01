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
}
