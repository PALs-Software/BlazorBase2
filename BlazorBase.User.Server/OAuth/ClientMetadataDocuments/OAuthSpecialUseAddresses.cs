using System.Net;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// The special-use address ranges (RFC 6890 and its IANA registries) a client id metadata document must
/// never be fetched from: loopback, private, link-local, shared, documentation, benchmarking, multicast
/// and reserved space. Fetching from them would let anyone who can name a client id probe the server's
/// own network (SSRF).
/// </summary>
public static class OAuthSpecialUseAddresses
{
    private static readonly IPNetwork[] Ranges =
    [
        IPNetwork.Parse("0.0.0.0/8"),
        IPNetwork.Parse("10.0.0.0/8"),
        IPNetwork.Parse("100.64.0.0/10"),
        IPNetwork.Parse("127.0.0.0/8"),
        IPNetwork.Parse("169.254.0.0/16"),
        IPNetwork.Parse("172.16.0.0/12"),
        IPNetwork.Parse("192.0.0.0/24"),
        IPNetwork.Parse("192.0.2.0/24"),
        IPNetwork.Parse("192.88.99.0/24"),
        IPNetwork.Parse("192.168.0.0/16"),
        IPNetwork.Parse("198.18.0.0/15"),
        IPNetwork.Parse("198.51.100.0/24"),
        IPNetwork.Parse("203.0.113.0/24"),
        IPNetwork.Parse("224.0.0.0/4"),
        IPNetwork.Parse("240.0.0.0/4"),
        IPNetwork.Parse("::/96"),
        IPNetwork.Parse("::ffff:0:0:0/96"),
        IPNetwork.Parse("64:ff9b::/96"),
        IPNetwork.Parse("64:ff9b:1::/48"),
        IPNetwork.Parse("100::/64"),
        IPNetwork.Parse("2001::/23"),
        IPNetwork.Parse("2001:db8::/32"),
        IPNetwork.Parse("2002::/16"),
        IPNetwork.Parse("3fff::/20"),
        IPNetwork.Parse("5f00::/16"),
        IPNetwork.Parse("fc00::/7"),
        IPNetwork.Parse("fe80::/10"),
        IPNetwork.Parse("fec0::/10"),
        IPNetwork.Parse("ff00::/8"),
    ];

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="address"/> lies in a special-use range. An
    /// IPv4-mapped IPv6 address is judged by the IPv4 address it carries.
    /// </summary>
    public static bool Contains(IPAddress address)
    {
        var effectiveAddress = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return Ranges.Any(range => range.Contains(effectiveAddress));
    }
}
