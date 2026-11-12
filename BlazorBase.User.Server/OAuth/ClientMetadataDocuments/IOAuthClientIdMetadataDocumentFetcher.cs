namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>Fetches the OAuth Client ID Metadata Document a client id URL points at.</summary>
public interface IOAuthClientIdMetadataDocumentFetcher
{
    /// <summary>
    /// Fetches and parses the document at <paramref name="clientIdUrl"/>, which has already passed
    /// <see cref="OAuthClientIdMetadataDocumentUrl.Validate"/>. Never throws for a network or document
    /// problem; the result says what went wrong.
    /// </summary>
    Task<OAuthClientIdMetadataDocumentFetchResult> FetchAsync(string clientIdUrl, CancellationToken cancellationToken = default);
}
