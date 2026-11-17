namespace BlazorBase.User.Server.OAuth.Protocol;

/// <summary>
/// The host names treated as loopback, where plain <c>http</c> is acceptable because the traffic
/// never leaves the machine (RFC 8252 section 7.3).
/// </summary>
public static class OAuthLoopbackHosts
{
    private static readonly string[] Hosts = ["localhost", "127.0.0.1", "[::1]", "::1"];

    /// <summary>Returns <see langword="true"/> for <c>localhost</c>, <c>127.0.0.1</c> and <c>[::1]</c>.</summary>
    public static bool IsLoopback(string host) => Hosts.Contains(host, StringComparer.OrdinalIgnoreCase);
}
