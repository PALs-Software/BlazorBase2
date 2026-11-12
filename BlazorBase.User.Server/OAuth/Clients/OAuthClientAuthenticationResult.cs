using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Protocol;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The outcome of authenticating a client at the token or revocation endpoint: the client, or the error
/// to answer with. <see cref="UsedBasicAuthentication"/> decides whether an <c>invalid_client</c> answer
/// must carry <c>WWW-Authenticate: Basic</c> (RFC 6749 section 5.2).
/// </summary>
public sealed record OAuthClientAuthenticationResult(OAuthClient? Client, OAuthProtocolError? Error, bool UsedBasicAuthentication)
{
    public static OAuthClientAuthenticationResult Success(OAuthClient client, bool usedBasicAuthentication) =>
        new(client, null, usedBasicAuthentication);

    public static OAuthClientAuthenticationResult Failure(string error, string description, bool usedBasicAuthentication) =>
        new(null, new OAuthProtocolError(error, description), usedBasicAuthentication);
}
