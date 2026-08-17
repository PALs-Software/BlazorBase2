using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

/// <summary>
/// The behaviour that separates this implementation from a cache-authoritative one: what a second
/// process sees after the first has created or revoked a token.
/// </summary>
/// <remarks>
/// An implementation that preloads every token once and then trusts its cache passes every
/// single-instance test and still fails all four of these. Because the failures only appear with
/// more than one process, they would surface for the first time in production, as "the new token
/// gets a 401" and "the revoked token still works" - reported by users, not by a test run.
/// </remarks>
public class AccessTokenCrossInstanceTests
{
    [Fact]
    public async Task Token_created_on_one_instance_is_accepted_by_another()
    {
        using var host = new AccessTokenTestHost();

        var minting = AccessTokenTestHost.ResolveService(host.CreateInstance());
        var validating = AccessTokenTestHost.ResolveService(host.CreateInstance());

        var created = await minting.CreateAsync("ci-runner", expiresAt: null);

        var validated = await validating.ValidateAsync(created.Secret);

        Assert.NotNull(validated);
        Assert.Equal(created.Id, validated.TokenId);
    }

    [Fact]
    public async Task Revoking_on_one_instance_takes_effect_on_another_immediately()
    {
        using var host = new AccessTokenTestHost();

        var revoking = AccessTokenTestHost.ResolveService(host.CreateInstance());
        var validatingInstance = host.CreateInstance();

        var created = await revoking.CreateAsync("ci-runner", expiresAt: null);

        var beforeRevoke = await AccessTokenTestHost.ResolveService(validatingInstance).ValidateAsync(created.Secret);
        Assert.NotNull(beforeRevoke);

        await revoking.RevokeAsync(created.Id);

        var afterRevoke = await AccessTokenTestHost.ResolveService(validatingInstance).ValidateAsync(created.Secret);

        Assert.Null(afterRevoke);
    }

    [Fact]
    public async Task Expiry_written_by_another_instance_is_honoured_on_the_next_request()
    {
        using var host = new AccessTokenTestHost();

        var service = AccessTokenTestHost.ResolveService(host.CreateInstance());
        var validatingInstance = host.CreateInstance();

        var created = await service.CreateAsync("short-lived", DateTime.UtcNow.AddMinutes(5));

        Assert.NotNull(await AccessTokenTestHost.ResolveService(validatingInstance).ValidateAsync(created.Secret));

        await using (var dbContext = host.CreateDbContext())
        {
            var entity = await dbContext.AccessTokens.FirstAsync(accessToken => accessToken.Id == created.Id);
            entity.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        Assert.Null(await AccessTokenTestHost.ResolveService(validatingInstance).ValidateAsync(created.Secret));
    }

    [Fact]
    public async Task A_cached_token_is_still_re_checked_against_the_database()
    {
        using var host = new AccessTokenTestHost();

        var instance = host.CreateInstance();
        var service = AccessTokenTestHost.ResolveService(instance);

        var created = await service.CreateAsync("warm-cache", expiresAt: null);

        Assert.NotNull(await AccessTokenTestHost.ResolveService(instance).ValidateAsync(created.Secret));

        await using (var dbContext = host.CreateDbContext())
        {
            var entity = await dbContext.AccessTokens.FirstAsync(accessToken => accessToken.Id == created.Id);
            entity.IsRevoked = true;
            await dbContext.SaveChangesAsync();
        }

        Assert.Null(await AccessTokenTestHost.ResolveService(instance).ValidateAsync(created.Secret));
    }
}
