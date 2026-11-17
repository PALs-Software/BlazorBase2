using BlazorBase.User.Server.OAuth.Clients;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// Judges a fetched client id metadata document (draft-ietf-oauth-client-id-metadata-document sections
/// 4 to 6, and the MCP requirement of <c>redirect_uris</c>). The server supports such clients only as
/// public clients: a document must allow <c>none</c> at the token endpoint, and one that mentions a
/// shared secret is refused outright.
/// </summary>
public static class OAuthClientIdMetadataDocumentValidator
{
    private const int MaximumClientNameLength = 200;
    private const string CodeResponseType = "code";

    private static readonly string[] SharedSecretMethods = ["client_secret_post", "client_secret_basic", "client_secret_jwt"];

    /// <summary>Validates <paramref name="document"/>, fetched from <paramref name="clientIdUrl"/>, under <paramref name="options"/>.</summary>
    public static OAuthClientIdMetadataDocumentValidationResult Validate(string clientIdUrl, OAuthClientIdMetadataDocument document, OAuthServerOptions options)
    {
        if (!string.Equals(document.ClientId, clientIdUrl, StringComparison.Ordinal))
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("The document's client_id does not equal the URL it was fetched from.");

        if (document.ClientSecret is not null || document.ClientSecretExpiresAt is not null)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("A client id metadata document must not contain client_secret or client_secret_expires_at.");

        if (document.Jwks is not null && document.JwksUri is not null)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("A client id metadata document must not contain both jwks and jwks_uri.");

        var authenticationViolation = ValidateAuthenticationMethods(document);

        if (authenticationViolation is not null)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid(authenticationViolation);

        var grantTypes = (document.GrantTypes ?? [OAuthGrantTypes.AuthorizationCode])
            .Where(grantType => OAuthGrantTypes.All.Contains(grantType))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!grantTypes.Contains(OAuthGrantTypes.AuthorizationCode))
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("The document's grant_types must include authorization_code.");

        if (!(document.ResponseTypes ?? [CodeResponseType]).Contains(CodeResponseType))
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("The document's response_types must include code.");

        var redirectUris = document.RedirectUris ?? [];

        if (redirectUris.Count == 0)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid("The document must list at least one redirect URI in redirect_uris.");

        if (redirectUris.Count > OAuthRedirectUriValidator.MaximumRedirectUriCount)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid($"The document must not list more than {OAuthRedirectUriValidator.MaximumRedirectUriCount} redirect URIs.");

        var redirectUriViolation = redirectUris
            .Select(redirectUri => OAuthRedirectUriValidator.Validate(redirectUri, options))
            .FirstOrDefault(violation => violation is not null);

        if (redirectUriViolation is not null)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid(redirectUriViolation);

        var clientName = string.IsNullOrWhiteSpace(document.ClientName) ? new Uri(clientIdUrl).Host : document.ClientName.Trim();

        if (clientName.Length > MaximumClientNameLength)
            return OAuthClientIdMetadataDocumentValidationResult.Invalid($"The document's client_name must not exceed {MaximumClientNameLength} characters.");

        return OAuthClientIdMetadataDocumentValidationResult.Valid(clientName, redirectUris.Distinct(StringComparer.Ordinal).ToList(), grantTypes);
    }

    /// <summary>
    /// Section 5: <c>token_endpoint_auth_methods_supported</c> wins over the singular field, which must
    /// then be one of its values; with neither, the method is <c>none</c> (never RFC 7591's
    /// <c>client_secret_basic</c> default).
    /// </summary>
    private static string? ValidateAuthenticationMethods(OAuthClientIdMetadataDocument document)
    {
        var supportedMethods = document.TokenEndpointAuthMethodsSupported;
        var declaredMethod = document.TokenEndpointAuthMethod;

        if (supportedMethods is not null && declaredMethod is not null && !supportedMethods.Contains(declaredMethod))
            return "The document's token_endpoint_auth_method is not one of its token_endpoint_auth_methods_supported.";

        var effectiveMethods = supportedMethods ?? (declaredMethod is null ? [OAuthClientAuthenticationMethods.None] : [declaredMethod]);

        if (effectiveMethods.Any(method => SharedSecretMethods.Contains(method)))
            return "A client id metadata document must not declare a shared-secret token endpoint authentication method.";

        if (!effectiveMethods.Contains(OAuthClientAuthenticationMethods.None))
            return "This server accepts client id metadata document clients only with token endpoint authentication method none.";

        return null;
    }
}
