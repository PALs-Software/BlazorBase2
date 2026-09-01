namespace BlazorBase.User.Wasm;

/// <summary>
/// Cookie and header names for the CSRF double-submit check. Must match the server's
/// <c>AuthCookieOptions</c>; a mismatch means every write is refused with a 403 and nothing says why.
/// </summary>
public interface ICsrfTokenOptions
{
    string CookieName { get; }

    string HeaderName { get; }
}

/// <inheritdoc />
public sealed class CsrfTokenOptions : ICsrfTokenOptions
{
    public string CookieName { get; set; } = "XSRF-TOKEN";

    public string HeaderName { get; set; } = "X-CSRF-TOKEN";
}
