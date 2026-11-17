using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Configuration;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// The rules a client id must satisfy to be fetched as an OAuth Client ID Metadata Document
/// (draft-ietf-oauth-client-id-metadata-document section 3), plus the server's own host policy.
/// </summary>
public static class OAuthClientIdMetadataDocumentUrl
{
    private const string HttpsPrefix = "https://";

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="clientId"/> is meant as a metadata document URL.
    /// A registered client id never starts with <c>https://</c>.
    /// </summary>
    public static bool IsCandidate(string clientId) => clientId.StartsWith(HttpsPrefix, StringComparison.Ordinal);

    /// <summary>
    /// Returns <see langword="null"/> when <paramref name="clientId"/> may be fetched under
    /// <paramref name="options"/>, otherwise a description of the rule it breaks.
    /// </summary>
    /// <remarks>
    /// Accepted: an absolute <c>https</c> URL of at most <see cref="OAuthClient.MaximumIdLength"/>
    /// characters with a path other than <c>/</c>, without user information, query, fragment, dot
    /// segments or whitespace, whose host is in
    /// <see cref="OAuthServerOptions.AllowedClientIdMetadataDocumentHosts"/> when that list is set. A
    /// query is only discouraged by the draft; it is refused here because it is not needed and makes
    /// two spellings of one client possible.
    /// </remarks>
    public static string? Validate(string clientId, OAuthServerOptions options)
    {
        if (!IsCandidate(clientId))
            return "A client id metadata document URL must use https.";

        if (clientId.Length > OAuthClient.MaximumIdLength)
            return $"A client id metadata document URL must not exceed {OAuthClient.MaximumIdLength} characters.";

        if (clientId.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            return "A client id metadata document URL must not contain whitespace or control characters.";

        if (clientId.Contains('#') || clientId.Contains('?'))
            return "A client id metadata document URL must not contain a query or fragment.";

        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var parsedUrl) || parsedUrl.Scheme != Uri.UriSchemeHttps)
            return "The client id is not an absolute https URL.";

        if (!string.IsNullOrEmpty(parsedUrl.UserInfo))
            return "A client id metadata document URL must not contain user information.";

        var pathStart = clientId.IndexOf('/', HttpsPrefix.Length);

        if (pathStart < 0 || pathStart == clientId.Length - 1)
            return "A client id metadata document URL must have a path other than '/'.";

        if (clientId[(pathStart + 1)..].Split('/').Any(IsDotSegment))
            return "A client id metadata document URL must not contain dot segments.";

        if (options.AllowedClientIdMetadataDocumentHosts.Count > 0 && !options.AllowedClientIdMetadataDocumentHosts.Contains(parsedUrl.Host, StringComparer.OrdinalIgnoreCase))
            return $"Client id metadata documents are not accepted from '{parsedUrl.Host}'.";

        return null;
    }

    private static bool IsDotSegment(string segment)
    {
        var decodedSegment = segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);
        return decodedSegment is "." or "..";
    }
}
