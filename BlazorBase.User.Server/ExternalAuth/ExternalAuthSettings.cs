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
    /// Tenant IDs (the token's <c>tid</c> claim) allowed to exchange a token, enforced in addition
    /// to - never instead of - issuer validation. Empty means single-tenant: the authority's own
    /// published issuer is the only thing that decides which tenant is trusted, and there is
    /// nothing left to allow-list - unless the discovered issuer turns out to be tenant-templated
    /// regardless, which <see cref="OidcExternalTokenValidator"/> rejects at runtime since a fixed
    /// authority URL is no proof the issuer it publishes is fixed too. Required, non-empty, when
    /// <see cref="Authority"/> is a multi-tenant endpoint (<c>/common</c>, <c>/organizations</c>,
    /// <c>/consumers</c>) - there the issuer template alone accepts a token from any Entra tenant
    /// in existence, so this is the only thing standing between "any organization in the world"
    /// and the tenants the application actually serves.
    /// </summary>
    public IList<string> AllowedTenantIds { get; set; } = [];

    /// <summary>
    /// Delegated permissions (the token's space-delimited <c>scp</c> claim) that make the token
    /// acceptable for this API. At least one of <see cref="RequiredScopes"/> or
    /// <see cref="RequiredAppRoles"/> must match, unless both are empty - which accepts any token
    /// that otherwise validates and should only be used while no downstream scope requirement has
    /// been decided yet.
    /// </summary>
    public IList<string> RequiredScopes { get; set; } = [];

    /// <summary>
    /// Application roles (the token's <c>roles</c> claim, used for app-only / client-credential
    /// tokens) that make the token acceptable for this API. See <see cref="RequiredScopes"/>.
    /// </summary>
    public IList<string> RequiredAppRoles { get; set; } = [];

    /// <summary>
    /// Whether <paramref name="authority"/> is a multi-tenant Entra endpoint whose own issuer
    /// varies per tenant - <c>/common</c>, <c>/organizations</c> or <c>/consumers</c> - as opposed
    /// to a single tenant's fixed issuer. Shared between <see cref="ExternalAuthSettingsValidator"/>
    /// and <see cref="OidcExternalTokenValidator"/> so both agree on when
    /// <see cref="AllowedTenantIds"/> is mandatory versus when it is enforced.
    /// </summary>
    public static bool IsMultiTenantAuthority(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
            return false;

        var segments = authority.Split('/', StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment =>
            segment.Equals("common", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("organizations", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("consumers", StringComparison.OrdinalIgnoreCase));
    }
}
