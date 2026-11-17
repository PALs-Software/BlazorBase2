using BlazorBase.User.Server.Entities;
using BlazorBase.User.Server.OAuth.Maintenance;
using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace BlazorBase.User.Server.Test.OAuth;

/// <summary>
/// One pass of <see cref="OAuthCleanupService{TUser}"/> removes exactly the rows that can no longer be
/// used and keeps everything still live.
/// </summary>
public sealed class OAuthCleanupServiceTests
{
    [Fact]
    public async Task RunCleanupAsync_removes_dead_rows_and_keeps_live_ones()
    {
        await using var host = await OAuthTestHost.StartAsync();
        var user = await host.SeedUserAsync("ada@example.test");
        var utcNow = host.TimeProvider.GetUtcNow().UtcDateTime;
        var twoDaysAgo = utcNow.AddDays(-2);

        var liveClient = new OAuthClient { Id = "bbc_live", ClientName = "Live", TokenEndpointAuthMethod = "none", CreatedAt = twoDaysAgo };
        var abandonedClient = new OAuthClient { Id = "bbc_abandoned", ClientName = "Abandoned", TokenEndpointAuthMethod = "none", CreatedAt = twoDaysAgo };
        var freshClient = new OAuthClient { Id = "bbc_fresh", ClientName = "Fresh", TokenEndpointAuthMethod = "none", CreatedAt = utcNow };
        var liveGrant = new OAuthGrant { Id = Guid.NewGuid(), ClientId = liveClient.Id, UserId = user.Id, Scope = "mcp", RedirectUri = OAuthTestHost.RedirectUri, CreatedAt = twoDaysAgo };
        var oldRevokedGrant = new OAuthGrant { Id = Guid.NewGuid(), ClientId = liveClient.Id, UserId = user.Id, Scope = "mcp", RedirectUri = OAuthTestHost.RedirectUri, CreatedAt = utcNow.AddDays(-40), RevokedAt = utcNow.AddDays(-31) };
        var recentlyRevokedGrant = new OAuthGrant { Id = Guid.NewGuid(), ClientId = liveClient.Id, UserId = user.Id, Scope = "mcp", RedirectUri = OAuthTestHost.RedirectUri, CreatedAt = twoDaysAgo, RevokedAt = utcNow.AddDays(-1) };

        var liveCodeId = Guid.NewGuid();
        var liveRefreshTokenId = Guid.NewGuid();
        var recentlyConsumedRefreshTokenId = Guid.NewGuid();
        var liveAccessTokenId = Guid.NewGuid();
        var personalAccessTokenId = Guid.NewGuid();

        await using (var dbContext = host.CreateDbContext())
        {
            dbContext.OAuthClients.AddRange(liveClient, abandonedClient, freshClient);
            dbContext.OAuthGrants.AddRange(liveGrant, oldRevokedGrant, recentlyRevokedGrant);
            dbContext.OAuthAuthorizationCodes.AddRange(
                new OAuthAuthorizationCode { Id = liveCodeId, CodeHash = "live-code", GrantId = liveGrant.Id, CodeChallenge = "challenge", ExpiresAt = utcNow.AddMinutes(1) },
                new OAuthAuthorizationCode { Id = Guid.NewGuid(), CodeHash = "old-code", GrantId = liveGrant.Id, CodeChallenge = "challenge", ExpiresAt = twoDaysAgo },
                new OAuthAuthorizationCode { Id = Guid.NewGuid(), CodeHash = "revoked-grant-code", GrantId = oldRevokedGrant.Id, CodeChallenge = "challenge", ExpiresAt = utcNow.AddMinutes(1) });
            dbContext.OAuthRefreshTokens.AddRange(
                new OAuthRefreshToken { Id = liveRefreshTokenId, TokenHash = "live-refresh", GrantId = liveGrant.Id, CreatedAt = utcNow, ExpiresAt = utcNow.AddDays(30) },
                new OAuthRefreshToken { Id = recentlyConsumedRefreshTokenId, TokenHash = "recently-consumed", GrantId = liveGrant.Id, CreatedAt = utcNow, ExpiresAt = utcNow.AddDays(30), ConsumedAt = utcNow.AddHours(-1) },
                new OAuthRefreshToken { Id = Guid.NewGuid(), TokenHash = "old-consumed", GrantId = liveGrant.Id, CreatedAt = twoDaysAgo, ExpiresAt = utcNow.AddDays(28), ConsumedAt = twoDaysAgo },
                new OAuthRefreshToken { Id = Guid.NewGuid(), TokenHash = "expired", GrantId = liveGrant.Id, CreatedAt = utcNow.AddDays(-40), ExpiresAt = twoDaysAgo });
            dbContext.AccessTokens.AddRange(
                new AccessToken { Id = liveAccessTokenId, Prefix = "test_live", TokenHash = "live", Name = "OAuth: Live", UserId = user.Id, OAuthGrantId = liveGrant.Id, ExpiresAt = utcNow.AddHours(1) },
                new AccessToken { Id = Guid.NewGuid(), Prefix = "test_old", TokenHash = "old", Name = "OAuth: Live", UserId = user.Id, OAuthGrantId = liveGrant.Id, ExpiresAt = twoDaysAgo },
                new AccessToken { Id = personalAccessTokenId, Prefix = "test_mine", TokenHash = "personal", Name = "Personal", UserId = user.Id, ExpiresAt = twoDaysAgo });
            await dbContext.SaveChangesAsync();
        }

        var cleanupService = host.Host.Services.GetRequiredService<OAuthCleanupService<TestUser>>();
        await cleanupService.RunCleanupAsync();

        await using var verificationContext = host.CreateDbContext();

        Assert.Equal(["bbc_fresh", "bbc_live"], (await verificationContext.OAuthClients.Select(client => client.Id).ToListAsync()).Order());
        Assert.Equal(new[] { liveGrant.Id, recentlyRevokedGrant.Id }.Order(), (await verificationContext.OAuthGrants.Select(grant => grant.Id).ToListAsync()).Order());
        Assert.Equal([liveCodeId], await verificationContext.OAuthAuthorizationCodes.Select(code => code.Id).ToListAsync());
        Assert.Equal(new[] { liveRefreshTokenId, recentlyConsumedRefreshTokenId }.Order(), (await verificationContext.OAuthRefreshTokens.Select(token => token.Id).ToListAsync()).Order());
        Assert.Equal(new[] { liveAccessTokenId, personalAccessTokenId }.Order(), (await verificationContext.AccessTokens.Select(token => token.Id).ToListAsync()).Order());
    }
}
