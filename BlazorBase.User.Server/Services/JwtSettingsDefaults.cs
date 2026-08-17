namespace BlazorBase.User.Server.Services;

/// <summary>
/// Single source of truth for the default JWT access-token and refresh-token lifetimes, used when
/// the host does not override the corresponding "JwtSettings:*" configuration keys.
/// </summary>
public static class JwtSettingsDefaults
{
    public const int AccessTokenExpirationMinutes = 15;
    public const int RefreshTokenExpirationDays = 30;
}
