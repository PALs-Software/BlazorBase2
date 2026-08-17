namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// The result of a successful token validation. <c>UserId</c> is <see langword="null"/> for tokens
/// that are not bound to an account.
/// </summary>
public record ValidatedAccessToken(Guid TokenId, string Name, string? UserId);
