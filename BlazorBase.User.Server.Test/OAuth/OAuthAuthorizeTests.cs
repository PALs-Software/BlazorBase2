using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Test.Infrastructure;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// <c>GET authorize</c>: an untrusted client or redirect URI is answered with a plain 400 and never
/// redirected to; every later error goes back to the validated redirect URI with <c>state</c> and
/// <c>iss</c>; a valid request reaches the consent page carrying a protected request.
/// </summary>
public sealed class OAuthAuthorizeTests
{
    [Fact]
    public async Task An_unknown_client_is_rejected_without_a_redirect()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters("bbc_unknown", OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier())));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task An_unregistered_redirect_uri_is_rejected_without_a_redirect()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));
        parameters["redirect_uri"] = "https://evil.example/callback";

        var response = await host.AuthorizeAsync(parameters);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task A_missing_redirect_uri_is_rejected_when_the_client_registered_several()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(redirectUris: [OAuthTestHost.RedirectUri, "https://claude.com/api/mcp/auth_callback"]);

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));
        parameters["redirect_uri"] = null;

        var response = await host.AuthorizeAsync(parameters);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("response_type", "token", "unsupported_response_type")]
    [InlineData("response_type", null, "unsupported_response_type")]
    [InlineData("code_challenge", null, "invalid_request")]
    [InlineData("code_challenge", "too-short", "invalid_request")]
    [InlineData("code_challenge_method", "plain", "invalid_request")]
    [InlineData("code_challenge_method", null, "invalid_request")]
    [InlineData("scope", "mcp admin", "invalid_scope")]
    [InlineData("resource", "https://other.example/api/mcp", "invalid_target")]
    [InlineData("resource", "http://localhost/api/other", "invalid_target")]
    public async Task An_invalid_parameter_is_reported_to_the_redirect_uri(string parameter, string? value, string expectedError)
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()), state: "state-123");
        parameters[parameter] = value;

        var response = await host.AuthorizeAsync(parameters);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(OAuthTestHost.RedirectUri + "?", location.AbsoluteUri);
        Assert.Equal(expectedError, OAuthTestHost.ReadQueryValue(location, "error"));
        Assert.Equal("state-123", OAuthTestHost.ReadQueryValue(location, "state"));
        Assert.Equal(OAuthTestHost.Issuer, OAuthTestHost.ReadQueryValue(location, "iss"));
    }

    [Theory]
    [InlineData("http://LOCALHOST/api/mcp/")]
    [InlineData("http://localhost/api/mcp")]
    [InlineData(null)]
    public async Task An_equivalent_or_missing_resource_is_accepted(string? resource)
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));
        parameters["resource"] = resource;

        var response = await host.AuthorizeAsync(parameters);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/oauth/consent?request=", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_valid_request_reaches_the_consent_page_which_can_read_it_back()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(clientName: "Claude");
        var user = await host.SeedUserAsync("ada@example.test");

        var request = await host.StartAuthorizationAsync(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));

        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/oauth/authorization-requests?request={Uri.EscapeDataString(request)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await host.IssueSessionTokenAsync(user));
        var response = await host.CreateClient().SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<OAuthAuthorizationRequestDetails>();
        Assert.Equal("Claude", details!.ClientName);
        Assert.Equal("claude.ai", details.RedirectHost);
        Assert.Equal("mcp", details.Scopes.Single().Name);
        Assert.False(string.IsNullOrEmpty(details.Scopes.Single().Description));
    }

    [Fact]
    public async Task A_missing_scope_means_every_configured_scope()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?>
        {
            ["Scopes:mcp"] = "Use the tools",
            ["Scopes:read"] = "Read data",
        });
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(codeVerifier));
        parameters["scope"] = null;
        var location = (await host.AuthorizeAsync(parameters)).Headers.Location!.OriginalString;
        var request = Uri.UnescapeDataString(location["/oauth/consent?request=".Length..]);

        var code = OAuthTestHost.ReadQueryValue(await host.ApproveAsync(user, request), "code")!;
        var tokens = await OAuthTestHost.ReadTokenPairAsync(await host.RedeemCodeAsync(clientId, code, codeVerifier));

        Assert.Equal("mcp read", tokens.Scope);
    }
}
