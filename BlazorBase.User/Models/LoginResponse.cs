namespace BlazorBase.User.Models;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// <see cref="DateTimeOffset"/> rather than <see cref="DateTime"/>: a bare <c>DateTime</c>
    /// serialises without its offset, so a client in a non-UTC timezone that forgot to check
    /// <c>Kind</c> would compare it against local time and refresh either far too early or never.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
