using BlazorBase.User.Server.OAuth.Protocol;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The outcome of <see cref="IOAuthClientService.RegisterAsync"/>: the registered client, or the RFC 7591
/// error (<c>invalid_redirect_uri</c>, <c>invalid_client_metadata</c>) that rejected it.
/// </summary>
public sealed record OAuthClientRegistrationResult(OAuthClientRegistrationResponse? Client, OAuthProtocolError? Error)
{
    /// <summary><see langword="true"/> when the client was stored.</summary>
    public bool Succeeded => Client is not null;

    public static OAuthClientRegistrationResult Success(OAuthClientRegistrationResponse client) => new(client, null);

    public static OAuthClientRegistrationResult Failure(string error, string description) => new(null, new OAuthProtocolError(error, description));
}
