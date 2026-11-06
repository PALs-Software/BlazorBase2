namespace BlazorBase.User.Models;

/// <summary>
/// Carries the refresh token of a bearer session. Under the cookie transport the cookie is the session and the value is empty.
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
