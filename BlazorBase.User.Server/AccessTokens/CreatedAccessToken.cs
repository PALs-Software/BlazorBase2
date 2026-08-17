namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// The result of successfully creating an access token: the persisted identity plus the one-time
/// plain-text secret, which is never retrievable again after this point - only its hash is stored.
/// </summary>
public record CreatedAccessToken(Guid Id, string Name, string Secret, DateTime? ExpiresAt);
