using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorBase.User.Models;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// The consent decision: only a signed-in user may answer, a denial goes back as <c>access_denied</c>,
/// an approval creates the grant and a code, <c>RequiredRoles</c> is enforced, and the endpoint keeps
/// its CSRF protection under the cookie transport.
/// </summary>
public sealed class OAuthConsentDecisionTests
{
    [Fact]
    public async Task An_unauthenticated_decision_is_refused()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var request = await host.StartAuthorizationAsync(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));

        var response = await host.DecideAsync(sessionToken: null, request, approved: true);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_denial_redirects_with_access_denied_and_creates_no_grant()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var request = await host.StartAuthorizationAsync(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()), state: "s1");

        var response = await host.DecideAsync(await host.IssueSessionTokenAsync(user), request, approved: false);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var redirect = new Uri((await response.Content.ReadFromJsonAsync<OAuthAuthorizationDecisionResponse>())!.RedirectUri);
        Assert.Equal("access_denied", OAuthTestHost.ReadQueryValue(redirect, "error"));
        Assert.Equal("s1", OAuthTestHost.ReadQueryValue(redirect, "state"));
        Assert.Equal(OAuthTestHost.Issuer, OAuthTestHost.ReadQueryValue(redirect, "iss"));
        Assert.Null(OAuthTestHost.ReadQueryValue(redirect, "code"));

        await using var dbContext = host.CreateDbContext();
        Assert.Empty(await dbContext.OAuthGrants.ToListAsync());
    }

    [Fact]
    public async Task An_approval_returns_a_code_and_stores_only_its_hash()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(redirectUris: ["https://claude.ai/callback?tenant=1"]);
        var user = await host.SeedUserAsync("ada@example.test");

        var parameters = OAuthTestHost.AuthorizeParameters(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()), state: "s2");
        parameters["redirect_uri"] = "https://claude.ai/callback?tenant=1";
        var location = (await host.AuthorizeAsync(parameters)).Headers.Location!.OriginalString;
        var request = Uri.UnescapeDataString(location["/oauth/consent?request=".Length..]);

        var redirect = await host.ApproveAsync(user, request);
        var code = OAuthTestHost.ReadQueryValue(redirect, "code");

        Assert.StartsWith("https://claude.ai/callback?tenant=1&code=", redirect.AbsoluteUri);
        Assert.False(string.IsNullOrEmpty(code));
        Assert.Equal("s2", OAuthTestHost.ReadQueryValue(redirect, "state"));
        Assert.Equal(OAuthTestHost.Issuer, OAuthTestHost.ReadQueryValue(redirect, "iss"));

        await using var dbContext = host.CreateDbContext();
        var grant = await dbContext.OAuthGrants.SingleAsync();
        var storedCode = await dbContext.OAuthAuthorizationCodes.SingleAsync();

        Assert.Equal(user.Id, grant.UserId);
        Assert.Equal(clientId, grant.ClientId);
        Assert.Equal("mcp", grant.Scope);
        Assert.Equal("http://localhost/api/mcp", grant.Resource);
        Assert.NotEqual(code, storedCode.CodeHash);
    }

    [Fact]
    public async Task A_tampered_request_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var user = await host.SeedUserAsync("ada@example.test");

        var response = await host.DecideAsync(await host.IssueSessionTokenAsync(user), "CfDJ8-not-a-protected-request", approved: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Required_roles_are_enforced_on_the_decision_and_the_details()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["RequiredRoles:0"] = "Admin" });
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("user@example.test", role: "User");
        var admin = await host.SeedUserAsync("admin@example.test", role: "Admin");
        var request = await host.StartAuthorizationAsync(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));

        var userSession = await host.IssueSessionTokenAsync(user);
        using var detailsMessage = new HttpRequestMessage(HttpMethod.Get, $"/api/oauth/authorization-requests?request={Uri.EscapeDataString(request)}");
        detailsMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userSession);

        var detailsResponse = await host.CreateClient().SendAsync(detailsMessage);
        var userDecision = await host.DecideAsync(userSession, request, approved: true);
        var adminDecision = await host.DecideAsync(await host.IssueSessionTokenAsync(admin), request, approved: true);

        Assert.Equal(HttpStatusCode.Forbidden, detailsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, userDecision.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adminDecision.StatusCode);
    }

    [Fact]
    public async Task The_decision_keeps_csrf_protection_under_the_cookie_transport()
    {
        await using var host = await OAuthTestHost.StartAsync(useCookies: true);
        var (clientId, _) = await host.RegisterClientAsync();
        var request = await host.StartAuthorizationAsync(clientId, OAuthTestHost.ComputeCodeChallenge(OAuthTestHost.CreateCodeVerifier()));

        var response = await host.DecideAsync(sessionToken: null, request, approved: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
