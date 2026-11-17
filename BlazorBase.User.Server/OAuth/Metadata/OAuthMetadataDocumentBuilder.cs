using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BlazorBase.User.Server.OAuth.Metadata;

/// <summary>
/// Builds the two discovery documents: the RFC 8414 authorization-server metadata and the RFC 9728
/// protected-resource metadata of each registered resource. Both are derived per request, because the
/// issuer may be.
/// </summary>
public sealed class OAuthMetadataDocumentBuilder(
    IOptionsMonitor<OAuthServerOptions> options,
    OAuthIssuerResolver issuerResolver)
{
    #region Injects
    private readonly IOptionsMonitor<OAuthServerOptions> Options = options;
    private readonly OAuthIssuerResolver IssuerResolver = issuerResolver;
    #endregion

    /// <summary>The well-known path of the authorization-server metadata (RFC 8414).</summary>
    public const string AuthorizationServerMetadataPath = "/.well-known/oauth-authorization-server";

    /// <summary>The well-known path prefix of the protected-resource metadata (RFC 9728).</summary>
    public const string ProtectedResourceMetadataPath = "/.well-known/oauth-protected-resource";

    /// <summary>Returns the RFC 8414 document for <paramref name="request"/>.</summary>
    public Dictionary<string, object> BuildAuthorizationServerMetadata(HttpRequest request)
    {
        var serverOptions = Options.CurrentValue;
        var issuer = IssuerResolver.Resolve(request);
        var endpointBase = $"{issuer}/{serverOptions.NormalizedEndpointBasePath}";

        var document = new Dictionary<string, object>
        {
            ["issuer"] = issuer,
            ["authorization_endpoint"] = $"{endpointBase}/authorize",
            ["token_endpoint"] = $"{endpointBase}/token",
        };

        if (serverOptions.EnableDynamicClientRegistration)
            document["registration_endpoint"] = $"{endpointBase}/register";

        document["revocation_endpoint"] = $"{endpointBase}/revoke";
        document["response_types_supported"] = new[] { "code" };
        document["grant_types_supported"] = OAuthGrantTypes.All;
        document["code_challenge_methods_supported"] = new[] { OAuthPkce.S256 };
        document["token_endpoint_auth_methods_supported"] = OAuthClientAuthenticationMethods.All;
        document["revocation_endpoint_auth_methods_supported"] = OAuthClientAuthenticationMethods.All;
        document["scopes_supported"] = serverOptions.Scopes.Keys.ToList();
        document["authorization_response_iss_parameter_supported"] = true;

        if (serverOptions.EnableClientIdMetadataDocuments)
            document["client_id_metadata_document_supported"] = true;

        return document;
    }

    /// <summary>Returns the RFC 9728 document of the protected resource at <paramref name="resourcePath"/>.</summary>
    public Dictionary<string, object> BuildProtectedResourceMetadata(HttpRequest request, string resourcePath)
    {
        var serverOptions = Options.CurrentValue;
        var issuer = IssuerResolver.Resolve(request);

        var document = new Dictionary<string, object>
        {
            ["resource"] = issuer + resourcePath,
            ["authorization_servers"] = new[] { issuer },
            ["scopes_supported"] = serverOptions.Scopes.Keys.ToList(),
            ["bearer_methods_supported"] = new[] { "header" },
        };

        if (!string.IsNullOrWhiteSpace(serverOptions.ResourceName))
            document["resource_name"] = serverOptions.ResourceName;

        return document;
    }

    /// <summary>The absolute URL of the protected-resource metadata of <paramref name="resourcePath"/>.</summary>
    public static string BuildProtectedResourceMetadataUrl(string issuer, string resourcePath) =>
        issuer + ProtectedResourceMetadataPath + resourcePath;
}
