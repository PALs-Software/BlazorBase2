using BlazorBase.User.Server.Entities;

namespace BlazorBase.User.Server.OAuth.Clients;

/// <summary>
/// The outcome of looking up a client id: the client, or why there is none - an unknown id, or a client
/// id metadata document that could not be fetched or was refused.
/// </summary>
public sealed record OAuthClientResolution(OAuthClient? Client, string? Error)
{
    public static OAuthClientResolution Found(OAuthClient client) => new(client, null);

    public static OAuthClientResolution NotFound(string error) => new(null, error);
}
