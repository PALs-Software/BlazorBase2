using System.Net;
using System.Net.Http.Headers;
using System.Text;
using BlazorBase.User.Server.AccessTokens;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// <c>POST token</c>: the full authorization-code flow ending in a call to the protected endpoint, every
/// way a code redemption must fail, client authentication per registered method, and refresh-token
/// rotation with reuse detection.
/// </summary>
public sealed class OAuthTokenEndpointTests
{
    [Fact]
    public async Task The_full_flow_issues_an_access_token_the_protected_endpoint_accepts()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(clientName: "Claude");
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var response = await host.RedeemCodeAsync(clientId, code, codeVerifier);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Contains("no-cache", response.Headers.Pragma.ToString());

        var tokens = await OAuthTestHost.ReadTokenPairAsync(response);
        Assert.Equal("mcp", tokens.Scope);
        Assert.Equal(3600, tokens.ExpiresIn);
        Assert.NotNull(tokens.RefreshToken);
        Assert.StartsWith("test_", tokens.AccessToken);

        var protectedResponse = await host.CallProtectedResourceAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, protectedResponse.StatusCode);
        Assert.Contains("OAuth: Claude", await protectedResponse.Content.ReadAsStringAsync());

        await using var dbContext = host.CreateDbContext();
        var accessToken = await dbContext.AccessTokens.SingleAsync();
        var grant = await dbContext.OAuthGrants.SingleAsync();
        Assert.Equal(grant.Id, accessToken.OAuthGrantId);
        Assert.Equal(user.Id, accessToken.UserId);
        Assert.NotNull(grant.LastUsedAt);
    }

    [Fact]
    public async Task OAuth_tokens_are_left_out_of_the_personal_token_list()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        await host.ObtainTokensAsync(user, clientId);

        using var scope = host.Host.Services.CreateScope();
        var accessTokenService = scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
        await accessTokenService.CreateAsync("personal", expiresAt: null, user.Id);

        var tokens = await accessTokenService.ListAsync(user.Id);

        Assert.Equal("personal", Assert.Single(tokens).Name);
    }

    [Fact]
    public async Task A_wrong_code_verifier_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var code = await host.ObtainCodeAsync(user, clientId, OAuthTestHost.CreateCodeVerifier());

        var response = await host.RedeemCodeAsync(clientId, code, OAuthTestHost.CreateCodeVerifier());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_redirect_uri_mismatch_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(redirectUris: [OAuthTestHost.RedirectUri, "https://claude.com/api/mcp/auth_callback"]);
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var response = await host.RedeemCodeAsync(clientId, code, codeVerifier, "https://claude.com/api/mcp/auth_callback");

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_resource_mismatch_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var response = await host.PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = OAuthTestHost.RedirectUri,
            ["resource"] = "https://other.example/api/mcp",
        });

        Assert.Equal("invalid_target", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_replayed_code_is_rejected_and_revokes_the_tokens_it_produced()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var first = await OAuthTestHost.ReadTokenPairAsync(await host.RedeemCodeAsync(clientId, code, codeVerifier));
        var replay = await host.RedeemCodeAsync(clientId, code, codeVerifier);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(replay));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(first.AccessToken)).StatusCode);
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(await host.RefreshAsync(clientId, first.RefreshToken!)));

        await using var dbContext = host.CreateDbContext();
        Assert.NotNull((await dbContext.OAuthGrants.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task An_expired_code_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        host.TimeProvider.Advance(TimeSpan.FromMinutes(3));
        var response = await host.RedeemCodeAsync(clientId, code, codeVerifier);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_code_issued_to_another_client_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var (otherClientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var response = await host.RedeemCodeAsync(otherClientId, code, codeVerifier);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_client_secret_post_client_authenticates_with_form_fields()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, clientSecret) = await host.RegisterClientAsync("client_secret_post");
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["client_secret"] = "wrong-secret",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = OAuthTestHost.RedirectUri,
        };

        var wrongSecret = await host.PostTokenAsync(form);
        form["client_secret"] = clientSecret!;
        var rightSecret = await host.PostTokenAsync(form);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
        Assert.Equal("invalid_client", await OAuthTestHost.ReadErrorAsync(wrongSecret));
        Assert.Empty(wrongSecret.Headers.WwwAuthenticate);
        Assert.Equal(HttpStatusCode.OK, rightSecret.StatusCode);
    }

    [Fact]
    public async Task A_client_secret_basic_client_authenticates_with_the_basic_header()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, clientSecret) = await host.RegisterClientAsync("client_secret_basic");
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = OAuthTestHost.RedirectUri,
        };

        var wrongSecret = await host.PostTokenAsync(form, Basic(clientId, "wrong-secret"));
        var formSecret = await host.PostTokenAsync(new Dictionary<string, string>(form) { ["client_id"] = clientId, ["client_secret"] = clientSecret! });
        var rightSecret = await host.PostTokenAsync(form, Basic(clientId, clientSecret!));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
        Assert.Equal("Basic", wrongSecret.Headers.WwwAuthenticate.Single().Scheme);
        Assert.Equal("invalid_client", await OAuthTestHost.ReadErrorAsync(formSecret));
        Assert.Equal(HttpStatusCode.OK, rightSecret.StatusCode);
    }

    [Fact]
    public async Task A_public_client_must_not_present_a_secret()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(user, clientId, codeVerifier);

        var response = await host.PostTokenAsync(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["client_secret"] = "anything",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = OAuthTestHost.RedirectUri,
        });

        Assert.Equal("invalid_client", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task An_unknown_grant_type_is_unsupported()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();

        var response = await host.PostTokenAsync(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = clientId });

        Assert.Equal("unsupported_grant_type", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task A_refresh_rotates_the_refresh_token()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var first = await host.ObtainTokensAsync(user, clientId);

        var response = await host.RefreshAsync(clientId, first.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var second = await OAuthTestHost.ReadTokenPairAsync(response);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(first.AccessToken, second.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(second.AccessToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.RefreshAsync(clientId, second.RefreshToken!)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_reused_within_the_grace_period_fails_without_revoking_the_grant()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var first = await host.ObtainTokensAsync(user, clientId);
        var second = await OAuthTestHost.ReadTokenPairAsync(await host.RefreshAsync(clientId, first.RefreshToken!));

        host.TimeProvider.Advance(TimeSpan.FromSeconds(10));
        var reuse = await host.RefreshAsync(clientId, first.RefreshToken!);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(reuse));
        Assert.Equal(HttpStatusCode.OK, (await host.RefreshAsync(clientId, second.RefreshToken!)).StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_reused_after_the_grace_period_revokes_the_grant()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var first = await host.ObtainTokensAsync(user, clientId);
        var second = await OAuthTestHost.ReadTokenPairAsync(await host.RefreshAsync(clientId, first.RefreshToken!));

        host.TimeProvider.Advance(TimeSpan.FromSeconds(31));
        var reuse = await host.RefreshAsync(clientId, first.RefreshToken!);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(reuse));
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(await host.RefreshAsync(clientId, second.RefreshToken!)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(second.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task An_expired_refresh_token_is_rejected()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);

        host.TimeProvider.Advance(TimeSpan.FromDays(31));
        var response = await host.RefreshAsync(clientId, tokens.RefreshToken!);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task An_inactive_owner_can_no_longer_refresh()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var tokens = await host.ObtainTokensAsync(user, clientId);

        await using (var dbContext = host.CreateDbContext())
        {
            var storedUser = await dbContext.Users.SingleAsync(entry => entry.Id == user.Id);
            storedUser.IsActive = false;
            await dbContext.SaveChangesAsync();
        }

        var response = await host.RefreshAsync(clientId, tokens.RefreshToken!);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task An_owner_who_lost_the_required_role_can_no_longer_refresh_and_the_grant_is_revoked()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["RequiredRoles:0"] = "Admin" });
        var (clientId, _) = await host.RegisterClientAsync();
        var admin = await host.SeedUserAsync("admin@example.test", role: "Admin");
        var tokens = await host.ObtainTokensAsync(admin, clientId);
        Assert.Equal(HttpStatusCode.OK, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);

        await host.RemoveFromRoleAsync(admin, "Admin");
        var response = await host.RefreshAsync(clientId, tokens.RefreshToken!);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CallProtectedResourceAsync(tokens.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task A_code_is_refused_when_the_owner_lost_the_required_role_before_redeeming_it()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["RequiredRoles:0"] = "Admin" });
        var (clientId, _) = await host.RegisterClientAsync();
        var admin = await host.SeedUserAsync("admin@example.test", role: "Admin");
        var codeVerifier = OAuthTestHost.CreateCodeVerifier();
        var code = await host.ObtainCodeAsync(admin, clientId, codeVerifier);

        await host.RemoveFromRoleAsync(admin, "Admin");
        var response = await host.RedeemCodeAsync(clientId, code, codeVerifier);

        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    [Fact]
    public async Task An_owner_who_still_holds_a_required_role_keeps_refreshing()
    {
        await using var host = await OAuthTestHost.StartAsync(new Dictionary<string, string?> { ["RequiredRoles:0"] = "Admin" });
        var (clientId, _) = await host.RegisterClientAsync();
        var admin = await host.SeedUserAsync("admin@example.test", role: "Admin");
        var tokens = await host.ObtainTokensAsync(admin, clientId);

        var response = await host.RefreshAsync(clientId, tokens.RefreshToken!);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_client_registered_without_the_refresh_grant_gets_no_refresh_token()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var (clientId, _) = await host.RegisterClientAsync(grantTypes: ["authorization_code"]);
        var user = await host.SeedUserAsync("ada@example.test");

        var tokens = await host.ObtainTokensAsync(user, clientId);

        Assert.Null(tokens.RefreshToken);
    }

    [Fact]
    public async Task The_token_endpoint_needs_no_antiforgery_token_under_the_cookie_transport()
    {
        await using var host = await OAuthTestHost.StartAsync(useCookies: true);
        var (clientId, _) = await host.RegisterClientAsync();

        var response = await host.PostTokenAsync(new Dictionary<string, string> { ["grant_type"] = "refresh_token", ["client_id"] = clientId, ["refresh_token"] = "unknown" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_grant", await OAuthTestHost.ReadErrorAsync(response));
    }

    private static AuthenticationHeaderValue Basic(string clientId, string clientSecret) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}")));
}
