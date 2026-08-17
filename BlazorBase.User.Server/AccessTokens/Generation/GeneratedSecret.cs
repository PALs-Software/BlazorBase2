namespace BlazorBase.User.Server.AccessTokens.Generation;

/// <summary>
/// The result of generating a new access-token secret, carrying the plain-text secret for
/// one-time delivery, its hash for persistence, and the non-secret prefix for lookups.
/// </summary>
public record GeneratedSecret(string Prefix, string Secret, string Hash);
