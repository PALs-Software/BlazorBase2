namespace BlazorBase.User.Server.Configuration;

/// <summary>
/// Controls how often <see cref="Services.RefreshTokenCleanupService{TUser}"/> sweeps the refresh-token
/// table for rows that are expired or revoked, so the table does not grow without bound.
/// </summary>
public class RefreshTokenCleanupOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(Services.AuthTokenDefaults.CleanupIntervalHours);
}
