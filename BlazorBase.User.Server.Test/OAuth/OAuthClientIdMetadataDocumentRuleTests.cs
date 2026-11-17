using System.Net;
using BlazorBase.User.Server.OAuth.ClientMetadataDocuments;
using BlazorBase.User.Server.OAuth.Configuration;
using BlazorBase.User.Server.OAuth.Protocol;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// The pure rules behind client id metadata documents: which URLs may be fetched, which addresses are
/// never connected to, and how a requested redirect URI matches a registered one.
/// </summary>
public sealed class OAuthClientIdMetadataDocumentRuleTests
{
    [Theory]
    [InlineData("https://claude.ai/oauth/client-metadata")]
    [InlineData("https://claude.ai:8443/oauth/client-metadata")]
    [InlineData("https://claude.ai/oauth/claude-code-client-metadata")]
    public void An_https_url_with_a_path_is_accepted(string clientId) =>
        Assert.Null(OAuthClientIdMetadataDocumentUrl.Validate(clientId, new OAuthServerOptions()));

    [Theory]
    [InlineData("https://claude.ai")]
    [InlineData("https://claude.ai/")]
    [InlineData("https://claude.ai/oauth/../metadata")]
    [InlineData("https://claude.ai/oauth/./metadata")]
    [InlineData("https://claude.ai/oauth/%2E%2E/metadata")]
    [InlineData("https://claude.ai/metadata?version=1")]
    [InlineData("https://claude.ai/metadata#fragment")]
    [InlineData("https://user:password@claude.ai/metadata")]
    [InlineData("https://claude.ai/meta data")]
    [InlineData("http://claude.ai/metadata")]
    public void A_url_breaking_the_draft_rules_is_refused(string clientId) =>
        Assert.NotNull(OAuthClientIdMetadataDocumentUrl.Validate(clientId, new OAuthServerOptions()));

    [Fact]
    public void A_url_longer_than_the_client_id_column_is_refused() =>
        Assert.NotNull(OAuthClientIdMetadataDocumentUrl.Validate("https://claude.ai/" + new string('a', 400), new OAuthServerOptions()));

    [Fact]
    public void The_host_allowlist_is_case_insensitive_and_exclusive()
    {
        var options = new OAuthServerOptions { AllowedClientIdMetadataDocumentHosts = ["Claude.ai"] };

        Assert.Null(OAuthClientIdMetadataDocumentUrl.Validate("https://claude.ai/oauth/client-metadata", options));
        Assert.NotNull(OAuthClientIdMetadataDocumentUrl.Validate("https://claude.ai.evil.example/oauth/client-metadata", options));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.0.10")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:192.168.0.10")]
    [InlineData("64:ff9b::a00:1")]
    [InlineData("2001:db8::1")]
    [InlineData("::7f00:1")]
    [InlineData("5f00::1")]
    public void Special_use_addresses_are_recognised(string address) =>
        Assert.True(OAuthSpecialUseAddresses.Contains(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://127.0.0.1:1/client-metadata")]
    [InlineData("https://localhost:1/client-metadata")]
    [InlineData("https://[::1]:1/client-metadata")]
    public async Task The_fetch_handler_never_connects_to_a_special_use_address(string url)
    {
        using var httpClient = new HttpClient(OAuthClientIdMetadataDocumentHttpHandler.Create());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => httpClient.GetAsync(url));

        Assert.IsType<OAuthForbiddenAddressException>(exception.InnerException);
    }

    [Theory]
    [InlineData("160.79.104.10")]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700::1111")]
    [InlineData("::ffff:8.8.8.8")]
    public void Public_addresses_are_allowed(string address) =>
        Assert.False(OAuthSpecialUseAddresses.Contains(IPAddress.Parse(address)));

    [Theory]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://claude.ai/api/mcp/auth_callback", true)]
    [InlineData("http://localhost/callback", "http://localhost:3118/callback", true)]
    [InlineData("http://127.0.0.1/callback", "http://127.0.0.1:50123/callback", true)]
    [InlineData("http://[::1]:8080/callback", "http://[::1]:9090/callback", true)]
    [InlineData("http://localhost/callback", "http://localhost:3118/other", false)]
    [InlineData("http://localhost/callback", "http://127.0.0.1:3118/callback", false)]
    [InlineData("http://localhost/callback", "http://localhost:3118/callback?x=1", false)]
    [InlineData("http://localhost/callback", "http://user@localhost:3118/callback", false)]
    [InlineData("https://claude.ai/api/mcp/auth_callback", "https://claude.ai:444/api/mcp/auth_callback", false)]
    [InlineData("https://localhost/callback", "https://localhost:3118/callback", false)]
    [InlineData("http://localhost/callback", "http://localhost:3118/./callback", false)]
    [InlineData("http://localhost/callback", "http://localhost:3118/%63allback", false)]
    [InlineData("http://localhost/callback", "http://LOCALHOST:3118/callback", false)]
    [InlineData("http://localhost/callback", "http://localhost:99999/callback", false)]
    [InlineData("http://localhost/callback", "http://localhost:3118/callback ", false)]
    [InlineData("http://localhost/callback", "http://localhost:3118/callback#x", false)]
    public void Only_loopback_http_redirect_uris_ignore_the_port(string registeredUri, string requestedUri, bool expected) =>
        Assert.Equal(expected, OAuthRedirectUriMatcher.Matches(registeredUri, requestedUri));
}
