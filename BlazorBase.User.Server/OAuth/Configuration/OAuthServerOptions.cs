namespace BlazorBase.User.Server.OAuth.Configuration;

/// <summary>
/// Configuration of the OAuth 2.1 authorization server registered by
/// <c>AddBlazorBaseOAuthServer</c>, bound from the <see cref="SectionName"/> configuration section and
/// validated at startup by <see cref="OAuthServerOptionsValidator"/>.
/// </summary>
public class OAuthServerOptions
{
    /// <summary>The configuration section the options are bound from.</summary>
    public const string SectionName = "OAuthServer";

    /// <summary>The scope advertised and granted when <see cref="Scopes"/> is left empty.</summary>
    public const string DefaultScope = "mcp";

    /// <summary>The description shown for <see cref="DefaultScope"/> when the host configures none.</summary>
    public const string DefaultScopeDescription = "Use this application's tools and data with your account.";

    /// <summary>
    /// The issuer identifier and base of every advertised endpoint URL, for example
    /// <c>https://app.example.com</c>. Must be absolute <c>https</c>, or <c>http</c> on a loopback host.
    /// Leave empty to derive it per request as <c>{scheme}://{host}{pathBase}</c>, which behind a reverse
    /// proxy is only right once forwarded headers are processed.
    /// </summary>
    public string? IssuerUri { get; set; }

    /// <summary>
    /// The path below which the flow endpoints (<c>register</c>, <c>authorize</c>, <c>token</c>,
    /// <c>revoke</c>) and the consent and grants API are mapped. Kept under <c>api/</c> by default because
    /// a BlazorBase PWA service worker answers every other navigation with the cached <c>index.html</c>,
    /// so a browser navigation to <c>authorize</c> would never reach the server.
    /// </summary>
    public string EndpointBasePath { get; set; } = "api/oauth";

    /// <summary>The client route of the consent page that <c>authorize</c> redirects the browser to.</summary>
    public string ConsentPagePath { get; set; } = "/oauth/consent";

    /// <summary>
    /// The scopes the server advertises and grants, name to description. Advertised and recorded only -
    /// an OAuth token acts with its owner's rights like a personal access token. Empty means the single
    /// scope <see cref="DefaultScope"/>.
    /// </summary>
    public Dictionary<string, string> Scopes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Roles of which the approving user must hold at least one; empty allows every signed-in user.</summary>
    public List<string> RequiredRoles { get; set; } = [];

    /// <summary>
    /// Maps the anonymous RFC 7591 <c>register</c> endpoint. When <see langword="false"/> clients are
    /// created by the host through <c>IOAuthClientService.RegisterAsync</c> instead.
    /// </summary>
    public bool EnableDynamicClientRegistration { get; set; } = true;

    /// <summary>
    /// Accepts an <c>https</c> URL as client id and loads the client from the OAuth Client ID Metadata
    /// Document published there (the registration MCP clients such as Claude prefer), advertised as
    /// <c>client_id_metadata_document_supported</c>.
    /// </summary>
    public bool EnableClientIdMetadataDocuments { get; set; } = true;

    /// <summary>
    /// Hosts a client id metadata document may be fetched from (case-insensitive); empty allows any host
    /// that resolves to a public address. Special-use addresses (RFC 6890) are refused either way.
    /// </summary>
    public List<string> AllowedClientIdMetadataDocumentHosts { get; set; } = [];

    /// <summary>How long a fetched metadata document is used at least, whatever its cache headers say.</summary>
    public TimeSpan ClientIdMetadataDocumentMinimumCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>How long a fetched metadata document is used at most before it is fetched again.</summary>
    public TimeSpan ClientIdMetadataDocumentMaximumCacheDuration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How long fetching one metadata document may take.</summary>
    public TimeSpan ClientIdMetadataDocumentFetchTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The most metadata document clients that have no grant yet. A new client id beyond it is refused
    /// until the cleanup service removes unused ones, so anonymous <c>authorize</c> requests cannot fill
    /// the client table.
    /// </summary>
    public int MaximumUnusedClientIdMetadataDocumentClients { get; set; } = 1000;

    /// <summary>Hosts a redirect URI may point at (case-insensitive); empty allows any host.</summary>
    public List<string> AllowedRedirectUriHosts { get; set; } = [];

    /// <summary>
    /// Private-use URI schemes (for example <c>com.example.app</c>) accepted for redirect URIs in
    /// addition to <c>https</c> and loopback <c>http</c>.
    /// </summary>
    public List<string> AdditionalRedirectUriSchemes { get; set; } = [];

    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan AuthorizationCodeLifetime { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How long the consent page may take between <c>authorize</c> and the user's decision.</summary>
    public TimeSpan AuthorizationRequestLifetime { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// How long after a refresh token was redeemed a second redemption is answered with a plain
    /// <c>invalid_grant</c> - a client retrying after a lost response - instead of revoking the grant
    /// as a detected token theft.
    /// </summary>
    public TimeSpan RefreshTokenReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Optional human-readable name published as <c>resource_name</c> in the protected-resource metadata.</summary>
    public string? ResourceName { get; set; }

    /// <summary>How often the cleanup service removes expired codes, tokens, grants and unused clients.</summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary><see cref="EndpointBasePath"/> without leading or trailing slashes.</summary>
    public string NormalizedEndpointBasePath => EndpointBasePath.Trim('/');

    /// <summary><see cref="IssuerUri"/> without a trailing slash, or <see langword="null"/> when it is derived per request.</summary>
    public string? NormalizedIssuerUri => string.IsNullOrWhiteSpace(IssuerUri) ? null : IssuerUri.Trim().TrimEnd('/');
}
