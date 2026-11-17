using System.Net;
using System.Net.Sockets;

namespace BlazorBase.User.Server.OAuth.ClientMetadataDocuments;

/// <summary>
/// Creates the primary handler of <see cref="OAuthClientIdMetadataDocumentFetcher.HttpClientName"/>.
/// The address check runs in the connect callback, on the addresses the socket is actually opened to,
/// so a host that resolves to a public address once and to an internal one later (DNS rebinding) is
/// still refused.
/// </summary>
public static class OAuthClientIdMetadataDocumentHttpHandler
{
    /// <summary>
    /// Returns a handler that follows no redirects (draft section 6), sends no cookies (section 11.2),
    /// bypasses any proxy (which would otherwise connect on the server's behalf, past the address
    /// check) and refuses every host with a special-use address.
    /// </summary>
    public static SocketsHttpHandler Create() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        AutomaticDecompression = DecompressionMethods.None,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        ConnectCallback = ConnectToPublicAddressAsync,
    };

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literalAddress)
            ? [literalAddress]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0 || addresses.Any(OAuthSpecialUseAddresses.Contains))
            throw new OAuthForbiddenAddressException(host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
