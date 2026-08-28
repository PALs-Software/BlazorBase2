namespace BlazorBase.User.Server.ExternalAuth;

/// <summary>
/// Configuration for validating an externally issued OIDC token (e.g. Microsoft Entra ID) before
/// exchanging it for a framework-issued access/refresh token pair. Bound from the
/// <see cref="SectionName"/> configuration section, then optionally overridden by the
/// <c>configureOptions</c> delegate passed to <c>AddBlazorBaseExternalAuth</c>.
/// </summary>
public class ExternalAuthSettings
{
    public const string SectionName = "ExternalAuthSettings";

    /// <summary>
    /// The OIDC authority to fetch signing keys and issuer metadata from, e.g.
    /// <c>https://login.microsoftonline.com/common/v2.0</c>. Required.
    /// </summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>
    /// Audience values the external token's <c>aud</c> claim must match one of. Required, at
    /// least one entry.
    /// </summary>
    public IList<string> Audiences { get; set; } = [];

    /// <summary>
    /// Whether the external token's <c>iss</c> claim is validated against the authority's
    /// published issuer. Defaults to <see langword="true"/>; a multi-tenant authority whose
    /// issuer varies per tenant (e.g. Entra ID's <c>/common</c> endpoint) sets this to
    /// <see langword="false"/> and relies on audience validation instead.
    /// </summary>
    public bool ValidateIssuer { get; set; } = true;
}
