using BlazorBase.User.Server.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

/// <summary>
/// A host-defined TPH-derived <c>AccessToken</c> type can be minted and validated through the same
/// single-insert, single-lookup path as the base type - the shape decided in Step 4b of the
/// PlatformSubscriptions migration plan.
/// </summary>
public class DerivedAccessTokenTests
{
    [Fact]
    public async Task Create_persists_the_derived_columns_in_the_same_insert_as_the_base_ones()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync<TestDerivedAccessToken>(
            "derived", expiresAt: null, token => token.Binding = "tenant-42");

        await using var dbContext = host.CreateDbContext();
        var stored = await dbContext.Set<TestDerivedAccessToken>().SingleAsync(t => t.Id == created.Id);

        Assert.Equal("tenant-42", stored.Binding);
        Assert.StartsWith(stored.Prefix, created.Secret);
    }

    [Fact]
    public async Task Validate_resolves_the_entity_as_its_derived_type()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync<TestDerivedAccessToken>(
            "derived", expiresAt: null, token => token.Binding = "tenant-42");

        var validated = await service.ValidateAsync(created.Secret);

        var derived = Assert.IsType<TestDerivedAccessToken>(validated!.Entity);
        Assert.Equal("tenant-42", derived.Binding);
    }

    [Fact]
    public async Task A_revoked_derived_token_does_not_validate()
    {
        using var host = new AccessTokenTestHost();
        var service = host.CreateService();

        var created = await service.CreateAsync<TestDerivedAccessToken>(
            "derived", expiresAt: null, token => token.Binding = "tenant-42");

        await service.RevokeAsync(created.Id);

        Assert.Null(await service.ValidateAsync(created.Secret));
    }
}
