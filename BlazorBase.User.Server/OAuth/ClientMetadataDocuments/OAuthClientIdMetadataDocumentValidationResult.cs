namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// A client id metadata document judged by <see cref="OAuthClientIdMetadataDocumentValidator"/>: the
/// client values to store, or the rule the document breaks.
/// </summary>
public sealed record OAuthClientIdMetadataDocumentValidationResult(
    string? Violation,
    string ClientName,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> GrantTypes)
{
    public bool IsValid => Violation is null;

    public static OAuthClientIdMetadataDocumentValidationResult Valid(string clientName, IReadOnlyList<string> redirectUris, IReadOnlyList<string> grantTypes) =>
        new(null, clientName, redirectUris, grantTypes);

    public static OAuthClientIdMetadataDocumentValidationResult Invalid(string violation) =>
        new(violation, string.Empty, [], []);
}
