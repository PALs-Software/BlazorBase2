using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Test.Infrastructure;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// RFC 7009 revocation by the client, and the user's connected-apps API: listing grants, revoking one's
/// own (with immediate effect on every token issued under it) and being refused another user's.
/// </summary>
public sealed class OAuthRevocationAndGrantsTests
{
    private static async Task<HttpResponseMessage> RevokeAsync(OAuthTestHost host, string clientId, string token) =>
        await host.CreateClient().PostAsync("/api/oauth/revoke", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["token"] = token,
        }));

    private static async Task<HttpResponseMessage> SendAsSessionAsync(OAuthTestHost host, string sessionToken, HttpMethod method, string path)
    {
        using var message = new HttpRequestMessage(method, path);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
        return await host.CreateClient().SendAsync(message);
    }

    [Fact]
    public async Task Revoking_a_refresh_token_revokes_its_whole_grant()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);

        var response = await RevokeAsync(host, clientId, tokens.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(await host.RefreshAsync(clientId, tokens.RefreshToken!)));
    }

    [Fact]
    public async Task Revoking_an_access_token_revokes_only_that_token()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);

        var response = await RevokeAsync(host, clientId, tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.RefreshAsync(clientId, tokens.RefreshToken!)).StatusCode);
    }

    [Fact]
    public async Task Another_client_cannot_revoke_a_token_and_unknown_tokens_answer_ok()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var (otherClientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);

        var foreign = await RevokeAsync(host, otherClientId, tokens.AccessToken);
        var unknown = await RevokeAsync(host, clientId, "not-a-token");

        Assert.Equal(HttpStatusCode.OK, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task The_user_lists_and_revokes_a_grant_and_its_tokens_stop_at_once()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(clientName: "Claude");
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);

        var sessionToken = await host.IssueSessionTokenAsync(user);
        var listResponse = await SendAsSessionAsync(host, sessionToken, HttpMethod.Get, "/api/oauth/grants");
        var grant = Assert.Single((await listResponse.Content.ReadFromJsonAsync<List<OAuthGrantSummary>>())!);

        Assert.Equal("Claude", grant.ClientName);
        Assert.Equal("claude.ai", grant.RedirectHost);
        Assert.Equal("mcp", grant.Scope);
        Assert.NotNull(grant.LastUsedAt);

        var revokeResponse = await SendAsSessionAsync(host, sessionToken, HttpMethod.Delete, $"/api/oauth/grants/{grant.Id}");
        var listAfterRevoke = await (await SendAsSessionAsync(host, sessionToken, HttpMethod.Get, "/api/oauth/grants")).Content.ReadFromJsonAsync<List<OAuthGrantSummary>>();

        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.Empty(listAfterRevoke!);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(await host.RefreshAsync(clientId, tokens.RefreshToken!)));
    }

    [Fact]
    public async Task Another_users_grant_cannot_be_revoked()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var owner = await host.SeedUserAsync("ada@example.test");
        var intruder = await host.SeedUserAsync("eve@example.test");
        var tokens = await host.ObtainTokensAsync(owner, clientId);

        var ownerGrants = await (await SendAsSessionAsync(host, await host.IssueSessionTokenAsync(owner), HttpMethod.Get, "/api/oauth/grants"))
            .Content.ReadFromJsonAsync<List<OAuthGrantSummary>>();
        var intruderSession = await host.IssueSessionTokenAsync(intruder);
        var intruderGrants = await (await SendAsSessionAsync(host, intruderSession, HttpMethod.Get, "/api/oauth/grants"))
            .Content.ReadFromJsonAsync<List<OAuthGrantSummary>>();

        var response = await SendAsSessionAsync(host, intruderSession, HttpMethod.Delete, $"/api/oauth/grants/{ownerGrants!.Single().Id}");

        Assert.Empty(intruderGrants!);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task An_oauth_access_token_can_neither_list_nor_revoke_grants_even_when_the_default_policy_accepts_it()
    {
        await using var host = await OAuthTestHost.StartAsync(acceptAccessTokensByDefault: true);
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);
        var grants = await (await SendAsSessionAsync(host, await host.IssueSessionTokenAsync(user), HttpMethod.Get, "/api/oauth/grants"))
            .Content.ReadFromJsonAsync<List<OAuthGrantSummary>>();

        var listResponse = await SendAsSessionAsync(host, tokens.AccessToken, HttpMethod.Get, "/api/oauth/grants");
        var revokeResponse = await SendAsSessionAsync(host, tokens.AccessToken, HttpMethod.Delete, $"/api/oauth/grants/{grants!.Single().Id}");

        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revokeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task The_grants_api_requires_a_signed_in_user()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CreateClient().GetAsync("/api/oauth/grants");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
