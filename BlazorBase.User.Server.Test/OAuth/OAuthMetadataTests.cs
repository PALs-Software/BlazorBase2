using System.Net;
using System.Text.Json;
using BlazorBase.User.Server.Test.Infrastructure;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// The discovery documents a claude.ai connector reads before anything else: RFC 8414 authorization-server
/// metadata and RFC 9728 protected-resource metadata, including the bare well-known alias, and the 401
/// challenge that points at them.
/// </summary>
public sealed class OAuthMetadataTests
{
    private static async Task<JsonElement> GetJsonAsync(OAuthTestHost host, string path)
    {
        var response = await host.CreateClient().GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("*", response.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    [Fact]
    public async Task Authorization_server_metadata_derives_the_issuer_from_the_request()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var metadata = await GetJsonAsync(host, "/.well-known/oauth-authorization-server");

        Assert.Equal("http://localhost", metadata.GetProperty("issuer").GetString());
        Assert.Equal("http://localhost/api/oauth/authorize", metadata.GetProperty("authorization_endpoint").GetString());
        Assert.Equal("http://localhost/api/oauth/token", metadata.GetProperty("token_endpoint").GetString());
        Assert.Equal("http://localhost/api/oauth/register", metadata.GetProperty("registration_endpoint").GetString());
        Assert.Equal("http://localhost/api/oauth/revoke", metadata.GetProperty("revocation_endpoint").GetString());
        Assert.Equal(["S256"], metadata.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["code"], metadata.GetProperty("response_types_supported").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal(["mcp"], metadata.GetProperty("scopes_supported").EnumerateArray().Select(value => value.GetString()));
        Assert.Contains("none", metadata.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(value => value.GetString()));
        Assert.True(metadata.GetProperty("authorization_response_iss_parameter_supported").GetBoolean());
    }

    [Fact]
    public async Task A_configured_issuer_wins_over_the_request()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["IssuerUri"] = "https://app.example.com/" });

        var metadata = await GetJsonAsync(host, "/.well-known/oauth-authorization-server");

        Assert.Equal("https://app.example.com", metadata.GetProperty("issuer").GetString());
        Assert.Equal("https://app.example.com/api/oauth/token", metadata.GetProperty("token_endpoint").GetString());
    }

    [Fact]
    public async Task Protected_resource_metadata_is_published_for_the_registered_path_and_the_bare_alias()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["ResourceName"] = "Test MCP" });

        foreach (var path in new[] { "/.well-known/oauth-protected-resource/api/mcp", "/.well-known/oauth-protected-resource" })
        {
            var metadata = await GetJsonAsync(host, path);

            Assert.Equal("http://localhost/api/mcp", metadata.GetProperty("resource").GetString());
            Assert.Equal(["http://localhost"], metadata.GetProperty("authorization_servers").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal(["header"], metadata.GetProperty("bearer_methods_supported").EnumerateArray().Select(value => value.GetString()));
            Assert.Equal("Test MCP", metadata.GetProperty("resource_name").GetString());
        }
    }

    [Fact]
    public async Task Disabled_dynamic_client_registration_is_neither_advertised_nor_mapped()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["EnableDynamicClientRegistration"] = "false" });

        var metadata = await GetJsonAsync(host, "/.well-known/oauth-authorization-server");
        var response = await host.RegisterAsync(new { redirect_uris = new[] { OAuthTestHost.RedirectUri } });

        Assert.False(metadata.TryGetProperty("registration_endpoint", out _));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_unauthenticated_call_to_a_registered_resource_is_challenged_with_its_metadata_url()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CallProtectedResourceAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/api/mcp\"",
            response.Headers.WwwAuthenticate.Single().ToString());
    }

    [Fact]
    public async Task An_unregistered_path_gets_a_plain_bearer_challenge()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CallProtectedResourceAsync(accessToken: null, OAuthTestHost.UnregisteredProtectedPath);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().ToString());
    }

    [Fact]
    public async Task A_rejected_token_is_challenged_with_invalid_token()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CallProtectedResourceAsync("test_notarealtokennotarealtoken");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(
            "Bearer error=\"invalid_token\", resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/api/mcp\"",
            response.Headers.WwwAuthenticate.Single().ToString());
    }
}
