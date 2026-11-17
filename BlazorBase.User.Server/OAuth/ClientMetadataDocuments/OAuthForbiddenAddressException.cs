namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// Thrown while connecting to a client id metadata document host that resolves to a special-use address
/// (<see cref="OAuthSpecialUseAddresses"/>), so the fetch fails before a single byte is sent.
/// </summary>
public sealed class OAuthForbiddenAddressException(string host)
    : IOException($"The host '{host}' resolves to a special-use address and is not fetched.")
{
}
