using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

/// <summary>
/// Lifecycle behaviour of <c>AccessTokenService</c> against a real relational provider.
/// </summary>
public class AccessTokenServiceTests
{
    [Fact]
    public async Task Create_returns_the_secret_once_and_stores_only_its_hash()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync("mcp", expiresAt: null);

        await using var dbContext = host.CreateDbContext();
        var stored = await dbContext.AccessTokens.SingleAsync();

        Assert.NotEqual(created.Secret, stored.TokenHash);
        Assert.DoesNotContain(created.Secret, stored.TokenHash);
        Assert.StartsWith(stored.Prefix, created.Secret);
    }

    [Fact]
    public async Task An_unknown_secret_does_not_validate()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        await service.CreateAsync("mcp", expiresAt: null);

        Assert.Null(await service.ValidateAsync("test_deadbeefdeadbeef"));
    }

    [Fact]
    public async Task An_already_expired_token_does_not_validate()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync("expired", DateTime.UtcNow.AddSeconds(-1));

        Assert.Null(await service.ValidateAsync(created.Secret));
    }

    [Fact]
    public async Task Revoke_reports_false_for_an_unknown_id()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        Assert.False(await service.RevokeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Revoke_scoped_to_an_owner_refuses_another_owners_token()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync("alice-token", expiresAt: null, userId: "alice");

        Assert.False(await service.RevokeAsync(created.Id, userId: "mallory"));
        Assert.NotNull(await service.ValidateAsync(created.Secret));

        Assert.True(await service.RevokeAsync(created.Id, userId: "alice"));
        Assert.Null(await service.ValidateAsync(created.Secret));
    }

    [Fact]
    public async Task Listing_scoped_to_an_owner_hides_other_owners_tokens()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        await service.CreateAsync("alice-token", expiresAt: null, userId: "alice");
        await service.CreateAsync("bob-token", expiresAt: null, userId: "bob");
        await service.CreateAsync("shared-token", expiresAt: null);

        var alices = await service.ListAsync("alice");

        Assert.Equal("alice-token", Assert.Single(alices).Name);
        Assert.Equal(3, (await service.ListAsync()).Count);
    }

    [Fact]
    public async Task A_listed_token_never_carries_its_hash()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync("mcp", expiresAt: null);

        var summary = Assert.Single(await service.ListAsync());

        Assert.Equal(created.Id, summary.Id);
        Assert.DoesNotContain(
            nameof(BlazorBase.User.Server.Entities.AccessToken.TokenHash),
            summary.GetType().GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task Touch_is_throttled_so_a_busy_token_does_not_write_per_request()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync("busy", expiresAt: null);

        await service.TouchAsync(created.Id);

        await using var dbContext = host.CreateDbContext();
        var firstTouch = (await dbContext.AccessTokens.AsNoTracking().SingleAsync()).LastUsedAt;

        Assert.NotNull(firstTouch);

        await service.TouchAsync(created.Id);

        var secondTouch = (await dbContext.AccessTokens.AsNoTracking().SingleAsync()).LastUsedAt;

        Assert.Equal(firstTouch, secondTouch);
    }

    /// <summary>
    /// A generated prefix must fit the column it is stored in. Asserted against the EF model rather
    /// than by writing an over-long value, because neither SQLite nor the InMemory provider enforces
    /// a string length - only SQL Server does, and only in production. The startup validator covers
    /// the configurable half of this; here the fixed half is pinned.
    /// </summary>
    [Fact]
    public async Task The_generated_prefix_fits_the_column_it_is_stored_in()
    {
        using var host = new AccessTokenTestHost();
        await using var dbContext = host.CreateDbContext();

        var prefixColumnLength = dbContext.Model
            .FindEntityType(typeof(BlazorBase.User.Server.Entities.AccessToken))!
            .FindProperty(nameof(BlazorBase.User.Server.Entities.AccessToken.Prefix))!
            .GetMaxLength();

        var created = await host.CreateService().CreateAsync("prefix-length", expiresAt: null);
        var storedPrefix = (await dbContext.AccessTokens.SingleAsync(accessToken => accessToken.Id == created.Id)).Prefix;

        Assert.NotNull(prefixColumnLength);
        Assert.True(
            storedPrefix.Length <= prefixColumnLength,
            $"Generated prefix '{storedPrefix}' is {storedPrefix.Length} characters, column holds {prefixColumnLength}.");
    }

    /// <summary>
    /// The hash must fit too - and this one has no configuration knob, so if the algorithm is ever
    /// swapped for one with a longer digest, this is what catches it.
    /// </summary>
    [Fact]
    public async Task The_stored_hash_fits_the_column_it_is_stored_in()
    {
        using var host = new AccessTokenTestHost();
        await using var dbContext = host.CreateDbContext();

        var hashColumnLength = dbContext.Model
            .FindEntityType(typeof(BlazorBase.User.Server.Entities.AccessToken))!
            .FindProperty(nameof(BlazorBase.User.Server.Entities.AccessToken.TokenHash))!
            .GetMaxLength();

        var created = await host.CreateService().CreateAsync("hash-length", expiresAt: null);
        var stored = await dbContext.AccessTokens.SingleAsync(accessToken => accessToken.Id == created.Id);

        Assert.NotNull(hashColumnLength);
        Assert.True(
            stored.TokenHash.Length <= hashColumnLength,
            $"Stored hash is {stored.TokenHash.Length} characters, column holds {hashColumnLength}.");
    }
}
