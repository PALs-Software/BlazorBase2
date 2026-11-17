namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// An OAuth error in the RFC 6749 shape: an <see cref="OAuthErrorCodes"/> value and a developer-facing
/// English description.
/// </summary>
public sealed record OAuthProtocolError(string Error, string Description);
