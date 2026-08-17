using BlazorBase.User.Server.AccessTokens.Caching;
using Xunit;

namespace BlazorBase.User.Server.Test.AccessTokens;

public class AccessTokenCacheTests
{
    private static CachedAccessToken CreateEntry(string prefix, string? name = null) =>
        new(Guid.NewGuid(), prefix, $"hash-{Guid.NewGuid()}", name ?? "token", null);

    [Fact]
    public void An_unknown_prefix_reports_a_miss_with_an_empty_list_rather_than_null()
    {
        var cache = new AccessTokenCache();

        Assert.False(cache.TryGetByPrefix("test_00000000", out var candidates));
        Assert.Empty(candidates);
    }

    /// <summary>
    /// The property that keeps the cache from becoming an unbounded, attacker-addressable memory
    /// sink: probing with random secrets produces a distinct non-existent prefix every time, and
    /// none of those lookups may leave anything behind.
    /// </summary>
    [Fact]
    public void A_miss_never_inserts_anything()
    {
        var cache = new AccessTokenCache();

        for (var attempt = 0; attempt < 1000; attempt++)
            cache.TryGetByPrefix($"test_{attempt:D8}", out _);

        var entry = CreateEntry("test_00000000");
        cache.Set(entry);
        cache.Remove(entry.Id);

        Assert.False(cache.TryGetByPrefix("test_00000000", out _));
    }

    [Fact]
    public void Two_tokens_sharing_a_prefix_are_both_kept_as_candidates()
    {
        var cache = new AccessTokenCache();

        cache.Set(CreateEntry("test_shared01"));
        cache.Set(CreateEntry("test_shared01"));

        Assert.True(cache.TryGetByPrefix("test_shared01", out var candidates));
        Assert.Equal(2, candidates.Count);
    }

    [Fact]
    public void Setting_the_same_token_twice_replaces_rather_than_duplicates_it()
    {
        var cache = new AccessTokenCache();
        var entry = CreateEntry("test_00000000", "before");

        cache.Set(entry);
        cache.Set(entry with { Name = "after" });

        Assert.True(cache.TryGetByPrefix("test_00000000", out var candidates));
        Assert.Equal("after", Assert.Single(candidates).Name);
    }

    [Fact]
    public void Removing_one_of_two_candidates_leaves_the_other()
    {
        var cache = new AccessTokenCache();
        var kept = CreateEntry("test_shared01");
        var removed = CreateEntry("test_shared01");

        cache.Set(kept);
        cache.Set(removed);
        cache.Remove(removed.Id);

        Assert.True(cache.TryGetByPrefix("test_shared01", out var candidates));
        Assert.Equal(kept.Id, Assert.Single(candidates).Id);
    }

    [Fact]
    public void Clear_discards_everything()
    {
        var cache = new AccessTokenCache();
        cache.Set(CreateEntry("test_00000000"));

        cache.Clear();

        Assert.False(cache.TryGetByPrefix("test_00000000", out _));
    }
}
