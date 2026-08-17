namespace BlazorBase.User.Server.AccessTokens;

/// <summary>
/// A non-secret projection of an access token for listing purposes. Never carries the hash.
/// </summary>
public record AccessTokenSummary(Guid Id, string Name, string? UserId, DateTime? ExpiresAt, DateTime? LastUsedAt, bool IsRevoked);
