namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// How fetching a client id metadata document ended. The draft separates failures that may heal on
/// their own (network, timeout, server error) from those that will not (a malformed or refused
/// document): only the latter may end the trust in a client that was already cached.
/// </summary>
public enum OAuthClientIdMetadataDocumentFetchStatus
{
    Success,
    TransientFailure,
    TerminalFailure,
}
