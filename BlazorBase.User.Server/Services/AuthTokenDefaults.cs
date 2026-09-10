namespace BlazorBase.User.Server.Services;

/// <summary>
/// Single source of truth for the default bearer-token and refresh-token lifetimes, used when the
/// host does not override the corresponding "AuthTokens:*" configuration keys.
/// </summary>
public static class AuthTokenDefaults
{
    public const int AccessTokenExpirationMinutes = 15;
    public const int RefreshTokenExpirationDays = 30;

    /// <summary>How often <see cref="RefreshTokenCleanupService{TUser}"/> sweeps expired and revoked rows.</summary>
    public const int CleanupIntervalHours = 24;

    /// <summary>
    /// Default for <c>AuthTokens:SecurityStampValidationInterval</c> - re-validate a bearer-authenticated
    /// caller's security stamp against the database on every request.
    /// </summary>
    public static readonly TimeSpan SecurityStampValidationInterval = TimeSpan.Zero;
}
