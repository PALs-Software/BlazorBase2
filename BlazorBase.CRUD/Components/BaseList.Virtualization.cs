using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Models;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The virtualized side of the list: rows are fetched from the data provider as chunks while the page scrolls, see
/// <see cref="ListRangeCache{TModel}"/>.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    private const int MaxCachedRows = 5000;
    private const int FetchDebounceMilliseconds = 60;
    private const int MaxFetchAttempts = 3;

    private ListRangeCache<TModel> RangeCache { get; set; } = new(DefaultChunkSize, MaxCachedRows / DefaultChunkSize);
    private int ServedStart { get; set; }
    private int ServedCount { get; set; }

    private const int DefaultChunkSize = 50;

    /// <summary>
    /// How many rows one request to the data provider asks for: <see cref="PageSize"/>, kept within what a provider
    /// accepts per request.
    /// </summary>
    private int ChunkSize => Math.Clamp(PageSize, 10, BaseQueryLimits.MaxPageSize);

    private int MaxCachedChunks => Math.Max(2, MaxCachedRows / ChunkSize);

    private bool HasRows => TotalCount > 0;

    private BaseQuery BuildQuery(int skip, int take)
    {
        var filters = new List<FilterDescriptor>(CurrentFilters);
        if (AdditionalFilters is not null)
            filters.AddRange(AdditionalFilters);

        return new BaseQuery
        {
            Filters = filters,
            Sorts = [.. EffectiveSorts],
            Select = BuildSelectList(),
            NavigationFilters = NavigationFilters,
            Skip = skip,
            Take = take
        };
    }

    /// <summary>
    /// Serves a range of rows to the grid or the tile view: from what is cached, otherwise from the chunks that are
    /// fetched for it.
    /// </summary>
    /// <remarks>
    /// The virtualizer cancels the request it made before whenever the visible range moves on, so a fast scroll
    /// produces many requests of which only the last one matters. The data grid already waits a moment before it
    /// asks, so only the tile view lets a request that has to fetch wait here and give up when it was cancelled in
    /// the meantime (<paramref name="debounce"/>); the fetches that do start are shared, so two requests that need
    /// the same chunk wait for one answer. <paramref name="remember"/> says whether the range is what is on screen.
    /// </remarks>
    internal async Task<List<TModel>> ReadRangeAsync(int startIndex, int count, CancellationToken cancellationToken, bool debounce, bool remember = true)
    {
        startIndex = Math.Max(0, startIndex);

        for (var attempt = 0; attempt < MaxFetchAttempts; attempt++)
        {
            var cache = RangeCache;

            if (cache.TryGetRange(startIndex, count, out var items))
            {
                if (remember)
                    NoteServedRange(cache, startIndex, items.Count);

                return items;
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (debounce && cache.ChunkCount > 0)
                await Task.Delay(FetchDebounceMilliseconds, cancellationToken);

            if (IsDisposed || !ReferenceEquals(cache, RangeCache))
                continue;

            var pending = StartFetches(cache, cache.MissingChunks(startIndex, count));

            if (pending.Count > 0)
                await Task.WhenAll(pending).WaitAsync(cancellationToken);
        }

        return [];
    }

    /// <summary>
    /// Remembers where the user is, so a reload can bring the same part of the list back, and reads ahead one chunk
    /// in the direction the list is being scrolled.
    /// </summary>
    private void NoteServedRange(ListRangeCache<TModel> cache, int startIndex, int servedCount)
    {
        var forward = startIndex >= ServedStart;

        ServedStart = startIndex;
        ServedCount = servedCount;

        if (TotalCount != cache.TotalCount)
        {
            TotalCount = cache.TotalCount;
            _ = InvokeAsync(StateHasChanged);
        }

        if (servedCount == 0 || cache.HoldsWholeList)
            return;

        var neighbour = forward
            ? cache.ChunkIndexOf(startIndex + servedCount - 1) + 1
            : cache.ChunkIndexOf(startIndex) - 1;

        if (cache.ChunkExists(neighbour) && !cache.HasChunk(neighbour))
            _ = StartFetches(cache, [neighbour]);
    }

    /// <summary>
    /// Starts the fetches for the chunks that are neither held nor already being fetched, grouping chunks that follow
    /// each other into one request, and returns every fetch the given chunks now wait for.
    /// </summary>
    private List<Task> StartFetches(ListRangeCache<TModel> cache, List<int> chunks)
    {
        var waiting = new List<Task>();
        var toStart = new List<int>();

        foreach (var chunk in chunks)
        {
            if (cache.InFlight.TryGetValue(chunk, out var running))
                waiting.Add(running);
            else
                toStart.Add(chunk);
        }

        var maxRun = Math.Max(1, BaseQueryLimits.MaxPageSize / cache.ChunkSize);

        for (var index = 0; index < toStart.Count;)
        {
            var length = 1;

            while (index + length < toStart.Count && length < maxRun && toStart[index + length] == toStart[index] + length)
                length++;

            var fetch = FetchChunksAsync(cache, toStart[index], length);

            for (var offset = 0; offset < length; offset++)
                cache.InFlight[toStart[index] + offset] = fetch;

            waiting.Add(fetch);
            index += length;
        }

        return waiting;
    }

    private async Task FetchChunksAsync(ListRangeCache<TModel> cache, int firstChunk, int chunkCount)
    {
        var skip = firstChunk * cache.ChunkSize;
        var take = chunkCount * cache.ChunkSize;

        try
        {
            var result = await ResolvedDataProvider.GetListAsync(BuildQuery(skip, take));

            if (!IsDisposed)
                cache.Store(skip, result.Items, result.TotalCount, take);
        }
        finally
        {
            for (var offset = 0; offset < chunkCount; offset++)
                cache.InFlight.Remove(firstChunk + offset);
        }
    }

    /// <summary>
    /// The chunk-aligned range a reload fetches so that the rows on screen come back with it.
    /// </summary>
    private (int Start, int Count) ResolveReloadWindow(bool keepPosition)
    {
        var chunkSize = ChunkSize;

        if (!keepPosition)
            return (0, chunkSize);

        var first = ServedStart / chunkSize;
        var last = (ServedStart + Math.Max(1, ServedCount) - 1) / chunkSize;
        var chunks = Math.Min(last - first + 1, Math.Max(1, BaseQueryLimits.MaxPageSize / chunkSize));

        return (first * chunkSize, chunks * chunkSize);
    }
}
