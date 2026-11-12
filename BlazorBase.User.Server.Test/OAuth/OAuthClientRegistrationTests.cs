using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BlazorBase.User.Server.OAuth.Protocol;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// RFC 7591 dynamic client registration: public and confidential clients, the redirect URI rules that
/// keep the server from becoming an open redirector, and rejected metadata.
/// </summary>
public sealed class OAuthClientRegistrationTests
{
    [Fact]
    public async Task A_public_client_is_registered_without_a_secret()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.RegisterAsync(new
        {
            client_name = "Claude",
            redirect_uris = new[] { OAuthTestHost.RedirectUri },
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            logo_uri = "https://claude.ai/logo.png",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var clientId = root.GetProperty("client_id").GetString()!;

        Assert.StartsWith(OAuthSecretGenerator.ClientIdPrefix, clientId);
        Assert.Equal(26, clientId.Length);
        Assert.Equal("Claude", root.GetProperty("client_name").GetString());
        Assert.Equal("none", root.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal(["code"], root.GetProperty("response_types").EnumerateArray().Select(value => value.GetString()));
        Assert.False(root.TryGetProperty("client_secret", out _));
        Assert.True(root.GetProperty("client_id_issued_at").GetInt64() > 0);
    }

    [Fact]
    public async Task A_confidential_client_gets_its_secret_once_and_only_the_hash_is_stored()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.RegisterAsync(new { redirect_uris = new[] { OAuthTestHost.RedirectUri } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        var secret = root.GetProperty("client_secret").GetString()!;

        Assert.Equal("client_secret_basic", root.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal(0, root.GetProperty("client_secret_expires_at").GetInt64());
        Assert.Equal(["authorization_code"], root.GetProperty("grant_types").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("Unnamed client", root.GetProperty("client_name").GetString());

        await using var dbContext = host.CreateDbContext();
        var stored = await dbContext.OAuthClients.SingleAsync();

        Assert.NotEqual(secret, stored.ClientSecretHash);
        Assert.Equal(OAuthSecretGenerator.Hash(secret), stored.ClientSecretHash);
    }

    [Theory]
    [InlineData("http://claude.ai/callback")]
    [InlineData("https://claude.ai/callback#fragment")]
    [InlineData("/relative/callback")]
    [InlineData("javascript:alert(1)")]
    [InlineData("com.example.app:/callback")]
    [InlineData("https://user:password@claude.ai/callback")]
    public async Task An_unacceptable_redirect_uri_is_rejected(string redirectUri)
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.RegisterAsync(new { redirect_uris = new[] { redirectUri }, token_endpoint_auth_method = "none" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_redirect_uri", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Theory]
    [InlineData("http://localhost:33418/callback")]
    [InlineData("http://127.0.0.1/callback")]
    [InlineData("http://[::1]:8080/callback")]
    [InlineData("https://claude.com/api/mcp/auth_callback")]
    public async Task Https_and_loopback_http_redirect_uris_are_accepted(string redirectUri)
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.RegisterAsync(new { redirect_uris = new[] { redirectUri }, token_endpoint_auth_method = "none" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_configured_private_use_scheme_is_accepted()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["AdditionalRedirectUriSchemes:0"] = "com.example.app" });

        var response = await host.RegisterAsync(new { redirect_uris = new[] { "com.example.app://callback" }, token_endpoint_auth_method = "none" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task The_host_allow_list_restricts_redirect_uris()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["AllowedRedirectUriHosts:0"] = "CLAUDE.AI" });

        var allowed = await host.RegisterAsync(new { redirect_uris = new[] { OAuthTestHost.RedirectUri }, token_endpoint_auth_method = "none" });
        var rejected = await host.RegisterAsync(new { redirect_uris = new[] { "https://evil.example/callback" }, token_endpoint_auth_method = "none" });

        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        Assert.Equal("invalid_redirect_uri", await OAuthTestHost.ReadErrorAsync(rejected));
    }

    [Fact]
    public async Task Missing_and_too_many_redirect_uris_are_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var missing = await host.RegisterAsync(new { client_name = "No redirect" });
        var tooMany = await host.RegisterAsync(new { redirect_uris = Enumerable.Range(0, 11).Select(index => $"https://claude.ai/callback/{index}").ToArray() });

        Assert.Equal("invalid_redirect_uri", await OAuthTestHost.ReadErrorAsync(missing));
        Assert.Equal("invalid_redirect_uri", await OAuthTestHost.ReadErrorAsync(tooMany));
    }

    [Theory]
    [InlineData("{\"redirect_uris\":[\"https://claude.ai/cb\"],\"token_endpoint_auth_method\":\"private_key_jwt\"}")]
    [InlineData("{\"redirect_uris\":[\"https://claude.ai/cb\"],\"grant_types\":[\"refresh_token\"]}")]
    [InlineData("{\"redirect_uris\":[\"https://claude.ai/cb\"],\"grant_types\":[\"client_credentials\"]}")]
    [InlineData("{\"redirect_uris\":[\"https://claude.ai/cb\"],\"response_types\":[\"token\"]}")]
    [InlineData("{\"redirect_uris\":\"https://claude.ai/cb\"}")]
    [InlineData("not json")]
    public async Task Invalid_client_metadata_is_rejected(string body)
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CreateClient().PostAsync("/api/oauth/register", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_client_metadata", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task Registration_does_not_need_an_antiforgery_token_under_the_cookie_transport()
    {
        await using var host = await OAuthTestHost.StartAsync(useCookies: true);

        var response = await host.CreateClient().PostAsJsonAsync("/api/oauth/register", new { redirect_uris = new[] { OAuthTestHost.RedirectUri }, token_endpoint_auth_method = "none" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
