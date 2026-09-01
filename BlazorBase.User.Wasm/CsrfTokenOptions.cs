namespace BlazorBase.User.Wasm;

/// <summary>
/// Header name for the antiforgery token. Must match the server's <c>AuthCookies:CsrfHeaderName</c>;
/// a mismatch means every write is refused with a 403 and nothing says why.
/// </summary>
public interface ICsrfTokenOptions
{
    string HeaderName { get; }
}

/// <inheritdoc />
public sealed class CsrfTokenOptions : ICsrfTokenOptions
{
    public string HeaderName { get; set; } = "X-CSRF-TOKEN";
}
