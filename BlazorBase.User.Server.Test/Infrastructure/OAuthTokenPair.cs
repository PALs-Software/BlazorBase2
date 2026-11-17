namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>The parts of a token-endpoint response the OAuth tests assert on.</summary>
public sealed record OAuthTokenPair(string AccessToken, string? RefreshToken, string Scope, long ExpiresIn);
