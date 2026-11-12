using BlazorBase.User.Server.AccessTokens;
using Microsoft.AspNetCore.Http;

namespace BlazorBase.User.Server.OAuth.Metadata;

/// <summary>
/// Adds <c>resource_metadata="{issuer}/.well-known/oauth-protected-resource{path}"</c> to the access-token
/// challenge of a request below a registered protected resource - the RFC 9728 discovery hook an OAuth
/// client such as a claude.ai connector follows after its first, unauthenticated call.
/// </summary>
public sealed class OAuthProtectedResourceChallengeParameterProvider(
    OAuthProtectedResourceRegistry registry,
    OAuthIssuerResolver issuerResolver) : IAccessTokenChallengeParameterProvider
{
    #region Injects
    private readonly OAuthProtectedResourceRegistry Registry = registry;
    private readonly OAuthIssuerResolver IssuerResolver = issuerResolver;
    #endregion

    /// <inheritdoc/>
    public IEnumerable<KeyValuePair<string, string>> GetParameters(HttpContext httpContext)
    {
        var resourcePath = Registry.FindMatchingResourcePath(httpContext.Request.Path);

        if (resourcePath is null)
            return [];

        var metadataUrl = OAuthMetadataDocumentBuilder.BuildProtectedResourceMetadataUrl(IssuerResolver.Resolve(httpContext.Request), resourcePath);
        return [new KeyValuePair<string, string>("resource_metadata", metadataUrl)];
    }
}
