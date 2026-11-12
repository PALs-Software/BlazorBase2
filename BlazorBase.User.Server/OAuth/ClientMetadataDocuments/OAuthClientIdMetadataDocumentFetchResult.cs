namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// The outcome of fetching a client id metadata document: the parsed document and how long the response
/// allows it to be cached (<see langword="null"/> when it states nothing), or the failure.
/// </summary>
public sealed record OAuthClientIdMetadataDocumentFetchResult(
    OAuthClientIdMetadataDocumentFetchStatus Status,
    OAuthClientIdMetadataDocument? Document,
    TimeSpan? CacheDuration,
    string? Error)
{
    public static OAuthClientIdMetadataDocumentFetchResult Success(OAuthClientIdMetadataDocument document, TimeSpan? cacheDuration) =>
        new(OAuthClientIdMetadataDocumentFetchStatus.Success, document, cacheDuration, null);

    public static OAuthClientIdMetadataDocumentFetchResult Transient(string error) =>
        new(OAuthClientIdMetadataDocumentFetchStatus.TransientFailure, null, null, error);

    public static OAuthClientIdMetadataDocumentFetchResult Terminal(string error) =>
        new(OAuthClientIdMetadataDocumentFetchStatus.TerminalFailure, null, null, error);
}
