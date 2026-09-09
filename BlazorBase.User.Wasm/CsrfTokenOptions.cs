namespace BlazorBase.User.Wasm;

/// <summary>
/// Header name for the antiforgery token. Must match the server's <c>AuthCookies:CsrfHeaderName</c>;
/// a mismatch means every write is refused with a 403 and nothing says why.
/// </summary>
public interface ICsrfTokenOptions
{
    string HeaderName { get; }

    /// <summary>
    /// The application's own API origin. <see cref="CsrfHeaderHandler"/> compares every request
    /// against it and only attaches credentials or the token when the request stays on it.
    /// </summary>
    Uri? ApiOrigin { get; }
}

/// <inheritdoc />
public sealed class CsrfTokenOptions : ICsrfTokenOptions
{
    public string HeaderName { get; set; } = "X-CSRF-TOKEN";

    public Uri? ApiOrigin { get; internal set; }
}
