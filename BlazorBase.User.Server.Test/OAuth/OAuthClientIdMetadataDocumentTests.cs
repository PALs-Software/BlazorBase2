using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Test.Infrastructure;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// Clients identified by an OAuth Client ID Metadata Document URL - how Claude prefers to register: the
/// server fetches the document, caches it as a client, shows its host on the consent page, refuses a
/// document that does not describe the URL it came from, and revokes the client's grants once a fetched
/// document can no longer be trusted.
/// </summary>
public sealed class OAuthClientIdMetadataDocumentTests
{
    private const string ClientIdUrl = "https://claude.ai/oauth/client-metadata";

    private static async Task<OAuthTestHost> StartWithDocumentAsync(IDictionary<string, string?>? oauthSettings = null, string? cacheControl = null)
    {
        var host = await OAuthTestHost.StartAsync(oauthSettings);
        host.MetadataDocuments.Serve(ClientIdUrl, StubMetadataDocumentHandler.CreateDocument(ClientIdUrl), cacheControl);
        return host;
    }

    private static string NewCodeChallenge() => OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier());

    [Fact]
    public async Task The_server_advertises_metadata_document_support()
    {
        await using var host = await OAuthTestHost.StartAsync();

        var response = await host.CreateClient().GetAsync("/.well-known/oauth-authorization-server");
        using var metadata = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(metadata.RootElement.GetProperty("client_id_metadata_document_supported").GetBoolean());
        Assert.Contains("none", metadata.RootElement.GetProperty("token_endpoint_auth_methods_supported").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task A_metadata_document_client_completes_the_flow_and_refreshes_without_refetching()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("cimd@example.com");

        var tokens = await host.ObtainTokensAsync(user, ClientIdUrl);
        var refreshResponse = await host.RefreshAsync(ClientIdUrl, tokens.RefreshToken!);
        var refreshedTokens = await OAuthTestHost.ReadTokenPairAsync(refreshResponse);
        var resourceResponse = await host.CallProtectedResourceAsync(refreshedTokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, resourceResponse.StatusCode);
        Assert.Equal(1, host.MetadataDocuments.RequestCount(ClientIdUrl));
        Assert.Equal("application/json", host.MetadataDocuments.LastAcceptHeader);

        await using var dbContext = host.CreateDbContext();
        var client = dbContext.OAuthClients.Single(storedClient => storedClient.Id == ClientIdUrl);
        Assert.True(client.IsMetadataDocumentClient);
        Assert.Equal("none", client.TokenEndpointAuthMethod);
        Assert.Equal("Claude", client.ClientName);
    }

    [Fact]
    public async Task The_consent_page_shows_the_host_that_published_the_document()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("consent@example.com");
        var request = await host.StartAuthorizationAsync(ClientIdUrl, NewCodeChallenge());

        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/oauth/authorization-requests?request={Uri.EscapeDataString(request)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await host.IssueSessionTokenAsync(user));
        var response = await host.CreateClient().SendAsync(message);
        var details = await response.Content.ReadFromJsonAsync<OAuthAuthorizationRequestDetails>();

        Assert.Equal("claude.ai", details!.ClientIdHost);
        Assert.Equal("claude.ai", details.RedirectHost);
    }

    [Fact]
    public async Task A_registered_client_shows_no_client_id_host()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var user = await host.SeedUserAsync("registered@example.com");
        var (clientId, _) = await host.RegisterClientAsync();
        var request = await host.StartAuthorizationAsync(clientId, NewCodeChallenge());

        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/oauth/authorization-requests?request={Uri.EscapeDataString(request)}");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await host.IssueSessionTokenAsync(user));
        var details = await (await host.CreateClient().SendAsync(message)).Content.ReadFromJsonAsync<OAuthAuthorizationRequestDetails>();

        Assert.Null(details!.ClientIdHost);
    }

    [Fact]
    public async Task Turning_the_feature_off_removes_the_advertisement_and_rejects_url_client_ids()
    {
        await using var host = await StartWithDocumentAsync(new Dictionary<string, string?> { ["EnableClientIdMetadataDocuments"] = "false" });

        var metadataResponse = await host.CreateClient().GetAsync("/.well-known/oauth-authorization-server");
        using var metadata = JsonDocument.Parse(await metadataResponse.Content.ReadAsStringAsync());
        var authorizeResponse = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.False(metadata.RootElement.TryGetProperty("client_id_metadata_document_supported", out _));
        Assert.Equal(HttpStatusCode.BadRequest, authorizeResponse.StatusCode);
        Assert.Equal(0, host.MetadataDocuments.RequestCount(ClientIdUrl));
    }

    [Fact]
    public async Task A_host_outside_the_allowlist_is_never_fetched()
    {
        const string foreignClientId = "https://evil.example/client-metadata";
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["AllowedClientIdMetadataDocumentHosts:0"] = "claude.ai" });
        host.MetadataDocuments.Serve(foreignClientId, StubMetadataDocumentHandler.CreateDocument(foreignClientId));

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(foreignClientId, NewCodeChallenge()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(0, host.MetadataDocuments.RequestCount(foreignClientId));
    }

    public static TheoryData<string, object> RefusedDocuments => new()
    {
        { "client_id mismatch", Modify(document => document["client_id"] = "https://claude.ai/oauth/other") },
        { "client secret", Modify(document => document["client_secret"] = "shared") },
        { "secret auth method", Modify(document => document["token_endpoint_auth_method"] = "client_secret_basic") },
        { "no none method", Modify(document => { document.Remove("token_endpoint_auth_method"); document["token_endpoint_auth_methods_supported"] = new[] { "private_key_jwt" }; }) },
        { "no redirect uris", Modify(document => document.Remove("redirect_uris")) },
        { "javascript redirect", Modify(document => document["redirect_uris"] = new[] { "javascript:alert(1)" }) },
        { "no authorization code", Modify(document => document["grant_types"] = new[] { "client_credentials" }) },
    };

    private static Dictionary<string, object> Modify(Action<Dictionary<string, object>> change)
    {
        var document = StubMetadataDocumentHandler.CreateDocument(ClientIdUrl);
        change(document);
        return document;
    }

    [Theory]
    [MemberData(nameof(RefusedDocuments))]
    public async Task A_document_that_breaks_a_rule_is_rejected_without_a_redirect(string reason, object document)
    {
        await using var host = await OAuthTestHost.StartAsync();
        host.MetadataDocuments.Serve(ClientIdUrl, document);

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, reason);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_document_that_cannot_be_fetched_rejects_the_authorization(HttpStatusCode statusCode)
    {
        await using var host = await OAuthTestHost.StartAsync();
        host.MetadataDocuments.Fail(ClientIdUrl, statusCode);

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task A_document_not_served_as_json_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        host.MetadataDocuments.Serve(ClientIdUrl, StubMetadataDocumentHandler.CreateDocument(ClientIdUrl), mediaType: "text/html");

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_document_larger_than_five_kilobytes_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        host.MetadataDocuments.Serve(ClientIdUrl, Modify(document => document["padding"] = new string('x', 6000)));

        var response = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_cached_document_is_fetched_again_once_its_cache_lifetime_ends()
    {
        await using var host = await StartWithDocumentAsync(cacheControl: "max-age=600");

        await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));
        await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));
        var countWithinLifetime = host.MetadataDocuments.RequestCount(ClientIdUrl);

        host.TimeProvider.Advance(TimeSpan.FromMinutes(11));
        await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(1, countWithinLifetime);
        Assert.Equal(2, host.MetadataDocuments.RequestCount(ClientIdUrl));
    }

    [Fact]
    public async Task A_cache_lifetime_below_the_minimum_is_raised_to_the_minimum()
    {
        await using var host = await StartWithDocumentAsync(cacheControl: "no-store");

        await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));
        host.TimeProvider.Advance(TimeSpan.FromMinutes(4));
        await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(1, host.MetadataDocuments.RequestCount(ClientIdUrl));
    }

    [Fact]
    public async Task An_unreachable_document_keeps_an_already_cached_client_usable()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("offline@example.com");
        var tokens = await host.ObtainTokensAsync(user, ClientIdUrl);

        host.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        host.MetadataDocuments.Unreachable(ClientIdUrl);
        var refreshResponse = await host.RefreshAsync(ClientIdUrl, tokens.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task A_connection_dropped_while_reading_keeps_an_already_cached_client_usable()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("dropped@example.com");
        var tokens = await host.ObtainTokensAsync(user, ClientIdUrl);

        host.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        host.MetadataDocuments.Respond(ClientIdUrl, () => throw new IOException("The response ended prematurely."));
        var refreshResponse = await host.RefreshAsync(ClientIdUrl, tokens.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task A_cached_client_is_not_used_indefinitely_while_its_document_stays_unreachable()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("outage@example.com");
        var tokens = await host.ObtainTokensAsync(user, ClientIdUrl);

        host.MetadataDocuments.Unreachable(ClientIdUrl);
        host.TimeProvider.Advance(TimeSpan.FromHours(49));
        var refreshResponse = await host.RefreshAsync(ClientIdUrl, tokens.RefreshToken!);

        Assert.Equal("invalid_client", await OAuthTestHost.ReadErrorAsync(refreshResponse));
    }

    [Fact]
    public async Task New_metadata_document_clients_are_refused_while_too_many_have_no_grant()
    {
        const string secondClientId = "https://claude.ai/oauth/second-client";
        await using var host = await StartWithDocumentAsync(new Dictionary<string, string?> { ["MaximumUnusedClientIdMetadataDocumentClients"] = "1" });
        host.MetadataDocuments.Serve(secondClientId, StubMetadataDocumentHandler.CreateDocument(secondClientId));

        var firstResponse = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));
        var secondResponse = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(secondClientId, NewCodeChallenge()));
        var repeatedFirstResponse = await host.AuthorizeAsync(OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge()));

        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, repeatedFirstResponse.StatusCode);
    }

    [Fact]
    public async Task A_decision_is_refused_when_the_refetched_document_no_longer_lists_the_redirect_uri()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("moved@example.com");
        var request = await host.StartAuthorizationAsync(ClientIdUrl, NewCodeChallenge());

        host.TimeProvider.Advance(TimeSpan.FromMinutes(6));
        host.MetadataDocuments.Serve(ClientIdUrl, StubMetadataDocumentHandler.CreateDocument(ClientIdUrl, ["https://claude.com/api/mcp/auth_callback"]));
        var decision = await host.DecideAsync(await host.IssueSessionTokenAsync(user), request, approved: true);

        Assert.Equal(HttpStatusCode.BadRequest, decision.StatusCode);
    }

    [Fact]
    public async Task A_document_that_no_longer_matches_its_url_revokes_the_clients_grants()
    {
        await using var host = await StartWithDocumentAsync();
        var user = await host.SeedUserAsync("revoked@example.com");
        var tokens = await host.ObtainTokensAsync(user, ClientIdUrl);

        host.TimeProvider.Advance(TimeSpan.FromMinutes(10));
        host.MetadataDocuments.Serve(ClientIdUrl, Modify(document => document["client_id"] = "https://claude.ai/oauth/other"));
        var refreshResponse = await host.RefreshAsync(ClientIdUrl, tokens.RefreshToken!);
        var resourceResponse = await host.CallProtectedResourceAsync(tokens.AccessToken);

        Assert.Equal("invalid_client", await OAuthTestHost.ReadErrorAsync(refreshResponse));
        Assert.Equal(HttpStatusCode.Unauthorized, resourceResponse.StatusCode);

        await using var dbContext = host.CreateDbContext();
        Assert.All(dbContext.OAuthGrants.Where(grant => grant.ClientId == ClientIdUrl).ToList(), grant => Assert.NotNull(grant.RevokedAt));
    }

    [Fact]
    public async Task A_loopback_redirect_uri_of_the_document_matches_on_any_port()
    {
        await using var host = await OAuthTestHost.StartAsync();
        host.MetadataDocuments.Serve(ClientIdUrl, StubMetadataDocumentHandler.CreateDocument(ClientIdUrl, ["http://localhost/callback"]));
        var user = await host.SeedUserAsync("loopback@example.com");

        var parameters = OAuthTestHost.AuthorizeParameters(ClientIdUrl, NewCodeChallenge());
        parameters["redirect_uri"] = "http://localhost:3118/callback";
        var response = await host.AuthorizeAsync(parameters);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.OriginalString;
        var redirect = await host.ApproveAsync(user, Uri.UnescapeDataString(location["/oauth/consent?request=".Length..]));

        Assert.Equal("http://localhost:3118/callback", redirect.GetLeftPart(UriPartial.Path));
    }
}
