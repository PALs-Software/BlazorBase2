namespace BlazorBase.User.Services;

/// <summary>
/// Holds the antiforgery request token for the current client session, in memory only.
/// </summary>
/// <remarks>
/// The token arrives in the body of <c>GET /api/auth/session</c> and is bound to the caller's
/// identity, so it deliberately never lands in a cookie: anything able to write cookies could
/// otherwise plant both halves of the check.
/// </remarks>
public sealed class CsrfTokenProvider
{
    public string? Token { get; private set; }

    public void Set(string? token) => Token = string.IsNullOrEmpty(token) ? null : token;

    public void Clear() => Token = null;
}
