namespace BlazorBase.User.Server.Security;

/// <summary>
/// Controls how often <see cref="BlazorBaseAccessTokenStampValidationMiddleware"/> re-checks a
/// bearer-authenticated caller's security stamp against the database, bound from
/// <c>AuthTokens:SecurityStampValidationInterval</c> the same way <c>AuthTokens:AccessTokenMinutes</c>
/// is bound.
/// </summary>
public class AccessTokenStampValidationOptions
{
    /// <summary>
    /// How long a caller's stamp, once validated, is trusted before it is checked again.
    /// <see cref="TimeSpan.Zero"/> (the default) re-checks on every authenticated bearer request -
    /// one indexed primary-key read, the same cost <c>AccessTokenService</c> already pays for every
    /// machine token. A positive interval trades that freshness for less database load. A negative
    /// value disables re-validation entirely, reproducing the pre-fix behavior exactly.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.Zero;

    /// <summary>Whether re-validation runs at all - <see langword="false"/> once <see cref="Interval"/> is negative.</summary>
    public bool Enabled => Interval >= TimeSpan.Zero;
}
