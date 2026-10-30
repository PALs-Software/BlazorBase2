using BlazorBase.CRUD.Components.Internal;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

public class ListRangeCacheTests
{
    private sealed class Row(int number)
    {
        public int Number { get; } = number;
    }

    private static List<Row> Rows(int from, int count) => [.. Enumerable.Range(from, count).Select(number => new Row(number))];

    [Fact]
    public void ARangeInsideAStoredChunk_IsServedFromIt()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 95, 10);

        var held = cache.TryGetRange(3, 4, out var items);

        Assert.True(held);
        Assert.Equal([3, 4, 5, 6], items.Select(row => row.Number));
    }

    [Fact]
    public void ARangeAcrossChunks_NeedsEveryChunk()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 95, 10);

        Assert.False(cache.TryGetRange(8, 5, out _));
        Assert.Equal([1], cache.MissingChunks(8, 5));

        cache.Store(10, Rows(10, 10), 95, 10);

        Assert.True(cache.TryGetRange(8, 5, out var items));
        Assert.Equal([8, 9, 10, 11, 12], items.Select(row => row.Number));
    }

    [Fact]
    public void AStoredAnswerOfSeveralChunks_IsSplitAtChunkBoundaries()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(20, Rows(20, 30), 95, 30);

        Assert.Equal(3, cache.ChunkCount);
        Assert.Empty(cache.MissingChunks(20, 30));
        Assert.Equal([1], cache.MissingChunks(10, 10));
    }

    [Fact]
    public void TheLastChunk_IsShorterAndStillComplete()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(90, Rows(90, 5), 95, 10);

        Assert.True(cache.TryGetRange(90, 10, out var items));
        Assert.Equal(5, items.Count);
        Assert.Empty(cache.MissingChunks(90, 10));
    }

    [Fact]
    public void ARangeBeyondTheTotal_IsEmptyAndNeedsNothing()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 25, 10);

        Assert.True(cache.TryGetRange(30, 10, out var items));
        Assert.Empty(items);
        Assert.Empty(cache.MissingChunks(30, 10));
        Assert.False(cache.ChunkExists(3));
        Assert.True(cache.ChunkExists(2));
    }

    [Fact]
    public void WithoutATotal_NothingCanBeServed()
    {
        var cache = new ListRangeCache<Row>(10, 100);

        Assert.False(cache.HasTotal);
        Assert.False(cache.TryGetRange(0, 10, out _));
        Assert.Equal([0], cache.MissingChunks(0, 10));
    }

    [Fact]
    public void AnAnswerWithMoreRowsThanWereAskedFor_IsTheWholeListOfAProviderThatDoesNotPage()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        var all = Rows(0, 35);
        cache.Store(0, all, 35, 10);

        Assert.True(cache.HoldsWholeList);
        Assert.Equal(35, cache.TotalCount);
        Assert.Empty(cache.MissingChunks(20, 10));
        Assert.True(cache.TryGetRange(30, 10, out var items));
        Assert.Equal(all.Skip(30), items);
    }

    [Fact]
    public void ADifferentTotal_DropsTheChunksTakenFromTheOldList()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 95, 10);
        cache.Store(10, Rows(10, 10), 95, 10);

        cache.Store(20, Rows(20, 10), 96, 10);

        Assert.Equal(1, cache.ChunkCount);
        Assert.Equal(96, cache.TotalCount);
        Assert.Equal([0], cache.MissingChunks(0, 10));
    }

    [Fact]
    public void BeyondItsCapacity_TheLeastRecentlyUsedChunkGoesFirst()
    {
        var cache = new ListRangeCache<Row>(10, 3);
        cache.Store(0, Rows(0, 10), 100, 10);
        cache.Store(10, Rows(10, 10), 100, 10);
        cache.Store(20, Rows(20, 10), 100, 10);
        cache.TryGetRange(0, 10, out _);

        cache.Store(30, Rows(30, 10), 100, 10);

        Assert.Empty(cache.MissingChunks(0, 10));
        Assert.Equal([1], cache.MissingChunks(10, 10));
        Assert.Equal(3, cache.ChunkCount);
    }

    [Fact]
    public void ResidentItems_ComeInListOrder()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(10, Rows(10, 10), 100, 10);
        cache.Store(0, Rows(0, 10), 100, 10);

        Assert.Equal(Enumerable.Range(0, 20), cache.ResidentItems.Select(row => row.Number));
    }

    [Fact]
    public void AChunkThatCameBackEmptyWithinTheTotal_IsNotAskedForAgain()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 30, 10);
        cache.Store(10, [], 30, 10);

        Assert.Empty(cache.MissingChunks(10, 10));
        Assert.True(cache.TryGetRange(10, 10, out var items));
        Assert.Empty(items);
    }

    private sealed class IdentifiedRow(int id, string name)
    {
        public int Id { get; } = id;

        public string Name { get; } = name;
    }

    private static List<IdentifiedRow> IdentifiedRows(int from, int count)
        => [.. Enumerable.Range(from, count).Select(id => new IdentifiedRow(id, $"old {id}"))];

    [Fact]
    public void ReplacingAHeldRow_ChangesOnlyThatRowAndKeepsPositionAndTotal()
    {
        var cache = new ListRangeCache<IdentifiedRow>(10, 100);
        cache.Store(0, IdentifiedRows(1, 10), 95, 10);
        cache.Store(10, IdentifiedRows(11, 10), 95, 10);
        var fresh = new IdentifiedRow(13, "new 13");

        var replaced = cache.TryReplace(fresh);

        Assert.True(replaced);
        Assert.Equal(95, cache.TotalCount);
        Assert.True(cache.TryGetRange(10, 5, out var items));
        Assert.Equal(["old 11", "old 12", "new 13", "old 14", "old 15"], items.Select(row => row.Name));
        Assert.Same(fresh, items[2]);
    }

    [Fact]
    public void ReplacingARowThatIsNotHeld_ChangesNothing()
    {
        var cache = new ListRangeCache<IdentifiedRow>(10, 100);
        cache.Store(0, IdentifiedRows(1, 10), 95, 10);

        var replaced = cache.TryReplace(new IdentifiedRow(70, "new 70"));

        Assert.False(replaced);
        Assert.Equal(1, cache.ChunkCount);
        Assert.All(cache.ResidentItems, row => Assert.StartsWith("old", row.Name));
    }

    [Fact]
    public void ReplacingARowOfAModelWithoutAnId_NeverMatches()
    {
        var cache = new ListRangeCache<Row>(10, 100);
        cache.Store(0, Rows(0, 10), 95, 10);

        Assert.False(cache.TryReplace(new Row(3)));
    }

    [Fact]
    public void ReplacingARowOfAWholeListAnswer_ReplacesItInAllFutureReads()
    {
        var cache = new ListRangeCache<IdentifiedRow>(10, 100);
        var whole = IdentifiedRows(1, 25);
        cache.Store(0, whole, 25, 10);
        var fresh = new IdentifiedRow(21, "new 21");

        var replaced = cache.TryReplace(fresh);

        Assert.True(replaced);
        Assert.True(cache.HoldsWholeList);
        Assert.True(cache.TryGetRange(20, 5, out var items));
        Assert.Same(fresh, items[0]);
        Assert.Equal("old 21", whole[20].Name);
    }
}
