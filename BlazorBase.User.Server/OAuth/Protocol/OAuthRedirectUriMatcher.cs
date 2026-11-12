using System.Text.RegularExpressions;
using BlazorBase.User.Server.OAuth.Clients;

namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// Decides whether a redirect URI named in an authorization request matches a registered one: by
/// ordinal string equality, except that a loopback <c>http</c> redirect URI may use any port (RFC 8252
/// section 7.3). Native clients such as Claude Code register <c>http://localhost/callback</c> and listen
/// on whichever port the operating system hands them.
/// </summary>
/// <remarks>
/// The loopback comparison works on the raw strings - only the <c>:port</c> after the host is cut out,
/// everything else must be ordinally equal - so a requested URI that would only match after
/// <see cref="Uri"/> normalization (dot segments, escapes, case, padding) is refused. The browser is
/// later sent to exactly the requested string, which therefore never differs from a registered URI in
/// anything but the port.
/// </remarks>
public static partial class OAuthRedirectUriMatcher
{
    private const int MaximumPort = 65535;

    /// <summary>Returns <see langword="true"/> when <paramref name="requestedUri"/> may be used for <paramref name="registeredUri"/>.</summary>
    public static bool Matches(string registeredUri, string requestedUri)
    {
        if (string.Equals(registeredUri, requestedUri, StringComparison.Ordinal))
            return true;

        if (requestedUri.Length > OAuthRedirectUriValidator.MaximumRedirectUriLength)
            return false;

        var registered = LoopbackHttpUri().Match(registeredUri);
        var requested = LoopbackHttpUri().Match(requestedUri);

        if (!registered.Success || !requested.Success || !IsValidPort(requested.Groups["port"]) || !IsValidPort(registered.Groups["port"]))
            return false;

        return string.Equals(registered.Groups["host"].Value, requested.Groups["host"].Value, StringComparison.Ordinal)
            && string.Equals(registered.Groups["rest"].Value, requested.Groups["rest"].Value, StringComparison.Ordinal);
    }

    private static bool IsValidPort(Group portGroup) =>
        !portGroup.Success || (int.TryParse(portGroup.Value, out var port) && port is > 0 and <= MaximumPort);

    [GeneratedRegex(@"\Ahttp://(?<host>localhost|127\.0\.0\.1|\[::1\])(?::(?<port>[0-9]{1,5}))?(?<rest>[/?][\x21-\x22\x24-\x7E]*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex LoopbackHttpUri();
}
