namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// The rows of a virtualized list that are currently held in the browser, kept in fixed-size chunks so that scrolling
/// asks the data provider for aligned pages and a row is fetched once however often it scrolls into view.
/// </summary>
/// <remarks>
/// One instance stands for one generation of the list's data: a new filter, sort or reload creates a new instance,
/// so a late answer to an old query can only ever land in a cache that is no longer used. The cache holds at most
/// <c>maxChunks</c> chunks and drops the least recently used one beyond that. A data provider that ignores
/// <see cref="Models.BaseQuery.Skip"/> and <see cref="Models.BaseQuery.Take"/> and hands back the whole list as one
/// page (the in-memory provider does) is recognised and its list is then served by index without further fetching.
/// Not thread safe: it is only touched from the component's synchronization context.
/// </remarks>
internal sealed class ListRangeCache<TModel> where TModel : class
{
    private readonly Dictionary<int, CachedChunk> Chunks = [];
    private readonly int MaxChunks;
    private long Clock;
    private IReadOnlyList<TModel>? WholeList;

    public ListRangeCache(int chunkSize, int maxChunks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, 1);

        ChunkSize = chunkSize;
        MaxChunks = Math.Max(2, maxChunks);
    }

    /// <summary>
    /// How many rows one chunk holds; every fetch starts and ends on a multiple of it.
    /// </summary>
    public int ChunkSize { get; }

    /// <summary>
    /// The number of rows the data source reported with its latest answer, or <c>-1</c> before the first one.
    /// </summary>
    public int TotalCount { get; private set; } = -1;

    public bool HasTotal => TotalCount >= 0;

    /// <summary>
    /// Whether the data provider returned the complete list instead of the page that was asked for.
    /// </summary>
    public bool HoldsWholeList => WholeList is not null;

    public int ChunkCount => Chunks.Count;

    /// <summary>
    /// The fetches that are under way for chunks of this generation, by chunk index.
    /// </summary>
    public Dictionary<int, Task> InFlight { get; } = [];

    /// <summary>
    /// The rows that are held right now, in list order.
    /// </summary>
    public IEnumerable<TModel> ResidentItems
    {
        get
        {
            if (WholeList is not null)
                return WholeList;

            return Chunks.OrderBy(pair => pair.Key).SelectMany(pair => pair.Value.Items);
        }
    }

    public int ChunkIndexOf(int index) => Math.Max(0, index) / ChunkSize;

    /// <summary>
    /// Takes over an answer of the data provider for the rows <paramref name="startIndex"/> up to
    /// <paramref name="startIndex"/> + <paramref name="requestedCount"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="startIndex"/> and <paramref name="requestedCount"/> are multiples of <see cref="ChunkSize"/>.
    /// An answer that carries more rows than the request could have matched is the complete list of a provider that
    /// does not page. When the total differs from the one before, the rows already held were taken from a list that
    /// has changed since, so they are dropped and only the new answer stays.
    /// </remarks>
    public void Store(int startIndex, IReadOnlyList<TModel> items, int totalCount, int requestedCount)
    {
        var expected = Math.Min(requestedCount, Math.Max(0, totalCount - startIndex));

        if (items.Count > expected)
        {
            WholeList = items;
            Chunks.Clear();
            TotalCount = items.Count;
            return;
        }

        if (HasTotal && TotalCount != totalCount)
            Chunks.Clear();

        TotalCount = totalCount;

        if (items.Count == 0)
            Chunks[ChunkIndexOf(startIndex)] = new CachedChunk([], ++Clock);

        for (var offset = 0; offset < items.Count; offset += ChunkSize)
        {
            var chunk = new TModel[Math.Min(ChunkSize, items.Count - offset)];

            for (var i = 0; i < chunk.Length; i++)
                chunk[i] = items[offset + i];

            Chunks[ChunkIndexOf(startIndex + offset)] = new CachedChunk(chunk, ++Clock);
        }

        Evict();
    }

    /// <summary>
    /// Puts <paramref name="item"/> in the place of the held row that stands for the same entity, leaving the position,
    /// the total and every other row as they are.
    /// </summary>
    /// <remarks>
    /// Rows are matched by <see cref="ItemIdentityComparer{TModel}"/>, so a model without an id never matches. A row
    /// that is not held (not loaded yet, or dropped again) is not added: the cache only ever describes rows the data
    /// source reported at their position.
    /// </remarks>
    /// <returns>Whether a held row was replaced.</returns>
    public bool TryReplace(TModel item)
    {
        var comparer = ItemIdentityComparer<TModel>.Instance;

        if (WholeList is not null)
        {
            for (var index = 0; index < WholeList.Count; index++)
            {
                if (!comparer.Equals(WholeList[index], item))
                    continue;

                var copy = new List<TModel>(WholeList);
                copy[index] = item;
                WholeList = copy;
                return true;
            }

            return false;
        }

        foreach (var chunk in Chunks.Values)
        {
            for (var index = 0; index < chunk.Items.Length; index++)
            {
                if (!comparer.Equals(chunk.Items[index], item))
                    continue;

                chunk.Items[index] = item;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The position of the held row that stands for <paramref name="item"/>, or <c>-1</c> when no held row does.
    /// </summary>
    /// <remarks>
    /// Rows are matched by <see cref="ItemIdentityComparer{TModel}"/>; the same instance is found without reading ids.
    /// </remarks>
    public int IndexOf(TModel item)
    {
        var comparer = ItemIdentityComparer<TModel>.Instance;

        if (WholeList is not null)
        {
            for (var index = 0; index < WholeList.Count; index++)
            {
                if (ReferenceEquals(WholeList[index], item))
                    return index;
            }

            for (var index = 0; index < WholeList.Count; index++)
            {
                if (comparer.Equals(WholeList[index], item))
                    return index;
            }

            return -1;
        }

        foreach (var pair in Chunks)
        {
            if (Array.IndexOf(pair.Value.Items, item) is var same and >= 0)
                return pair.Key * ChunkSize + same;
        }

        foreach (var pair in Chunks)
        {
            for (var index = 0; index < pair.Value.Items.Length; index++)
            {
                if (comparer.Equals(pair.Value.Items[index], item))
                    return pair.Key * ChunkSize + index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The chunks that would have to be fetched before <paramref name="count"/> rows from
    /// <paramref name="startIndex"/> on can be served.
    /// </summary>
    public List<int> MissingChunks(int startIndex, int count)
    {
        var missing = new List<int>();

        if (WholeList is not null || count <= 0)
            return missing;

        var first = ChunkIndexOf(startIndex);
        var last = ChunkIndexOf(LastIndex(startIndex, count));

        for (var chunk = first; chunk <= last; chunk++)
        {
            if (!Chunks.ContainsKey(chunk))
                missing.Add(chunk);
        }

        return missing;
    }

    /// <summary>
    /// Serves a range from what is held; false while a chunk of it is still missing.
    /// </summary>
    public bool TryGetRange(int startIndex, int count, out List<TModel> items)
    {
        items = [];

        if (!HasTotal)
            return false;

        startIndex = Math.Max(0, startIndex);
        var available = Math.Max(0, Math.Min(count, TotalCount - startIndex));

        if (available == 0)
            return true;

        if (WholeList is not null)
        {
            items = [.. WholeList.Skip(startIndex).Take(available)];
            return true;
        }

        var result = new List<TModel>(available);
        var end = startIndex + available;

        for (var chunkIndex = ChunkIndexOf(startIndex); chunkIndex * ChunkSize < end; chunkIndex++)
        {
            if (!Chunks.TryGetValue(chunkIndex, out var chunk))
                return false;

            chunk.Stamp = ++Clock;

            var chunkStart = chunkIndex * ChunkSize;
            var from = Math.Max(startIndex, chunkStart) - chunkStart;
            var to = Math.Min(end, chunkStart + ChunkSize) - chunkStart;

            for (var i = from; i < to && i < chunk.Items.Length; i++)
                result.Add(chunk.Items[i]);
        }

        items = result;
        return true;
    }

    /// <summary>
    /// Whether a chunk beyond the ones already held exists in the list.
    /// </summary>
    public bool ChunkExists(int chunkIndex) => chunkIndex >= 0 && HasTotal && chunkIndex * ChunkSize < TotalCount;

    public bool HasChunk(int chunkIndex) => WholeList is not null || Chunks.ContainsKey(chunkIndex);

    private int LastIndex(int startIndex, int count)
    {
        var last = startIndex + count - 1;

        return HasTotal ? Math.Min(last, Math.Max(0, TotalCount - 1)) : last;
    }

    private void Evict()
    {
        while (Chunks.Count > MaxChunks)
        {
            var oldest = Chunks.MinBy(pair => pair.Value.Stamp).Key;
            Chunks.Remove(oldest);
        }
    }

    private sealed class CachedChunk(TModel[] items, long stamp)
    {
        public TModel[] Items { get; } = items;

        public long Stamp { get; set; } = stamp;
    }
}
