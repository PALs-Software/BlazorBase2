using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Configuration;

/// <summary>
/// Validates <see cref="OAuthServerOptions"/> at startup, so a misconfigured issuer, path, scope or
/// lifetime fails the deployment instead of surfacing as a connector that silently cannot sign in.
/// </summary>
public class OAuthServerOptionsValidator : IValidateOptions<OAuthServerOptions>
{
    /// <summary>
    /// Schemes that must never be accepted as an additional redirect scheme: script and data URIs
    /// execute in the browser, and <c>http</c>/<c>https</c> already have their own, stricter rules.
    /// </summary>
    private static readonly string[] ForbiddenRedirectUriSchemes = ["http", "https", "javascript", "data", "vbscript", "file", "blob", "about"];

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OAuthServerOptions options)
    {
        var failures = new List<string>();

        ValidateIssuer(options, failures);
        ValidatePaths(options, failures);
        ValidateScopes(options, failures);
        ValidateLifetimes(options, failures);
        ValidateRedirectUriRules(options, failures);

        if (options.RequiredRoles.Any(string.IsNullOrWhiteSpace))
            failures.Add("OAuthServer:RequiredRoles must not contain empty role names.");

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateIssuer(OAuthServerOptions options, List<string> failures)
    {
        var issuer = options.NormalizedIssuerUri;

        if (issuer is null)
            return;

        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            failures.Add($"OAuthServer:IssuerUri '{options.IssuerUri}' is not an absolute URI.");
            return;
        }

        if (!string.IsNullOrEmpty(issuerUri.Query) || !string.IsNullOrEmpty(issuerUri.Fragment))
            failures.Add("OAuthServer:IssuerUri must not carry a query or fragment (RFC 8414).");

        var isHttps = issuerUri.Scheme == Uri.UriSchemeHttps;
        var isLoopbackHttp = issuerUri.Scheme == Uri.UriSchemeHttp && OAuthLoopbackHosts.IsLoopback(issuerUri.Host);

        if (!isHttps && !isLoopbackHttp)
            failures.Add("OAuthServer:IssuerUri must use https, or http on a loopback host (localhost, 127.0.0.1, [::1]).");
    }

    private static void ValidatePaths(OAuthServerOptions options, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(options.NormalizedEndpointBasePath))
            failures.Add("OAuthServer:EndpointBasePath must not be empty.");

        if (options.NormalizedEndpointBasePath.Contains('?') || options.NormalizedEndpointBasePath.Contains('#'))
            failures.Add("OAuthServer:EndpointBasePath must be a plain path without query or fragment.");

        var consentPagePath = options.ConsentPagePath;
        var isLocalPath = !string.IsNullOrWhiteSpace(consentPagePath)
            && consentPagePath.StartsWith('/')
            && !consentPagePath.StartsWith("//", StringComparison.Ordinal)
            && !consentPagePath.StartsWith("/\\", StringComparison.Ordinal)
            && !consentPagePath.Contains('?')
            && !consentPagePath.Contains('#');

        if (!isLocalPath)
            failures.Add("OAuthServer:ConsentPagePath must be a local path starting with a single '/', without query or fragment.");
    }

    private static void ValidateScopes(OAuthServerOptions options, List<string> failures)
    {
        if (options.Scopes.Count == 0)
            failures.Add("OAuthServer:Scopes must contain at least one scope.");

        foreach (var scope in options.Scopes.Keys.Where(scope => !OAuthScopes.IsValidScopeToken(scope)))
            failures.Add($"OAuthServer:Scopes contains '{scope}', which is not a valid scope token (RFC 6749 section 3.3).");
    }

    private static void ValidateLifetimes(OAuthServerOptions options, List<string> failures)
    {
        RequirePositive(options.AccessTokenLifetime, "AccessTokenLifetime", failures);
        RequirePositive(options.RefreshTokenLifetime, "RefreshTokenLifetime", failures);
        RequirePositive(options.AuthorizationCodeLifetime, "AuthorizationCodeLifetime", failures);
        RequirePositive(options.AuthorizationRequestLifetime, "AuthorizationRequestLifetime", failures);
        RequirePositive(options.CleanupInterval, "CleanupInterval", failures);

        if (options.RefreshTokenReuseGracePeriod < TimeSpan.Zero)
            failures.Add("OAuthServer:RefreshTokenReuseGracePeriod must not be negative.");

        RequirePositive(options.ClientIdMetadataDocumentMinimumCacheDuration, "ClientIdMetadataDocumentMinimumCacheDuration", failures);
        RequirePositive(options.ClientIdMetadataDocumentMaximumCacheDuration, "ClientIdMetadataDocumentMaximumCacheDuration", failures);
        RequirePositive(options.ClientIdMetadataDocumentFetchTimeout, "ClientIdMetadataDocumentFetchTimeout", failures);

        if (options.MaximumUnusedClientIdMetadataDocumentClients <= 0)
            failures.Add("OAuthServer:MaximumUnusedClientIdMetadataDocumentClients must be greater than zero.");

        if (options.ClientIdMetadataDocumentMinimumCacheDuration > options.ClientIdMetadataDocumentMaximumCacheDuration)
            failures.Add("OAuthServer:ClientIdMetadataDocumentMinimumCacheDuration must not exceed ClientIdMetadataDocumentMaximumCacheDuration.");
    }

    private static void RequirePositive(TimeSpan value, string optionName, List<string> failures)
    {
        if (value <= TimeSpan.Zero)
            failures.Add($"OAuthServer:{optionName} must be greater than zero.");
    }

    private static void ValidateRedirectUriRules(OAuthServerOptions options, List<string> failures)
    {
        foreach (var scheme in options.AdditionalRedirectUriSchemes)
        {
            if (string.IsNullOrWhiteSpace(scheme) || !Uri.CheckSchemeName(scheme))
            {
                failures.Add($"OAuthServer:AdditionalRedirectUriSchemes contains '{scheme}', which is not a valid URI scheme.");
                continue;
            }

            if (ForbiddenRedirectUriSchemes.Contains(scheme, StringComparer.OrdinalIgnoreCase))
                failures.Add($"OAuthServer:AdditionalRedirectUriSchemes must not contain '{scheme}'.");
        }

        if (options.AllowedRedirectUriHosts.Any(string.IsNullOrWhiteSpace))
            failures.Add("OAuthServer:AllowedRedirectUriHosts must not contain empty host names.");

        if (options.AllowedClientIdMetadataDocumentHosts.Any(string.IsNullOrWhiteSpace))
            failures.Add("OAuthServer:AllowedClientIdMetadataDocumentHosts must not contain empty host names.");
    }
}
