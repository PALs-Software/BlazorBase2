using BlazorBase.CRUD.Filtering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Everything a list needs to offer saved filters, presets and a default: the storage key, the store (when the host
/// registered one), the localized presets and the filter fields that decide what can still be applied.
/// </summary>
public sealed class SavedFilterContext
{
    public SavedFilterContext(
        string listKey,
        IListFilterStore? store,
        ListFilterDefaults? defaults,
        IReadOnlyList<FilterFieldMetadata> fields,
        IStringLocalizer modelLocalizer)
    {
        ListKey = listKey;
        Store = store;
        Fields = fields;
        Defaults = defaults;
        Presets = [.. (defaults?.Presets ?? []).Select(preset => ResolvePreset(preset, modelLocalizer))];
    }

    public string ListKey { get; }

    public IListFilterStore? Store { get; }

    public IReadOnlyList<FilterFieldMetadata> Fields { get; }

    public ListFilterDefaults? Defaults { get; }

    public IReadOnlyList<ResolvedFilterPreset> Presets { get; }

    /// <summary>
    /// True when there is anything to show: a store to save into or at least one preset.
    /// </summary>
    public bool IsAvailable => Store is not null || Presets.Count > 0;

    /// <summary>
    /// Builds the context for a list, resolving the optional store from DI. The storage key is the explicit
    /// <see cref="ListFilterDefaults.ListKey"/> or, by default, the model name plus the page route (see
    /// <see cref="SavedFilterKey"/>).
    /// </summary>
    public static SavedFilterContext Create(
        IServiceProvider services,
        Type modelType,
        ListFilterDefaults? defaults,
        IReadOnlyList<FilterFieldMetadata> fields,
        IStringLocalizer modelLocalizer,
        bool useStore,
        string? currentUri = null,
        string? baseUri = null)
    {
        var store = useStore ? services.GetService<IListFilterStore>() : null;
        var listKey = SavedFilterKey.Resolve(defaults?.ListKey, modelType, currentUri, baseUri);

        return new SavedFilterContext(listKey, store, defaults, fields, modelLocalizer);
    }

    /// <summary>
    /// The filter to apply when the list first loads: the user's saved default, otherwise the code default, otherwise
    /// null. A store that cannot be reached falls back to the code default instead of failing the list.
    /// </summary>
    public async Task<FilterGroupModel?> ResolveInitialFilterAsync(int revision = 0, CancellationToken cancellationToken = default)
    {
        var userDefault = await TryReadUserDefaultAsync(revision, cancellationToken);
        var filter = userDefault?.Filter ?? Defaults?.ResolveCodeDefault();

        if (filter is null)
            return null;

        var pruned = FilterModelPruner.Prune(filter, Fields);
        return userDefault is not null || FilterModelConverter.CountConditions(pruned) > 0 ? pruned : null;
    }

    /// <summary>
    /// The user's saved filters of this list, read from the store once per <paramref name="revision"/>: the default
    /// filter of the first load and the quick filter dropdown share one request. A save, delete or default change
    /// raises the revision, and a read that failed is tried again.
    /// </summary>
    public Task<IReadOnlyList<SavedListFilterInfo>> ReadSavedAsync(int revision)
    {
        if (Store is null)
            return Task.FromResult<IReadOnlyList<SavedListFilterInfo>>([]);

        if (CachedRead is null || CachedRevision != revision || CachedRead.IsFaulted || CachedRead.IsCanceled)
        {
            CachedRevision = revision;
            CachedRead = Store.GetAsync(ListKey);
        }

        return CachedRead;
    }

    private Task<IReadOnlyList<SavedListFilterInfo>>? CachedRead { get; set; }

    private int CachedRevision { get; set; } = -1;

    private async Task<SavedListFilterInfo?> TryReadUserDefaultAsync(int revision, CancellationToken cancellationToken)
    {
        if (Store is null)
            return null;

        try
        {
            var saved = await ReadSavedAsync(revision).WaitAsync(cancellationToken);
            return saved.FirstOrDefault(filter => filter.IsDefault);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    private static ResolvedFilterPreset ResolvePreset(FilterPreset preset, IStringLocalizer modelLocalizer)
    {
        var localized = modelLocalizer[$"FilterPreset_{preset.Id}"];
        var name = localized.ResourceNotFound ? preset.Name : localized.Value;

        return new ResolvedFilterPreset(preset.Id, name, preset.Filter, preset.IsDefault);
    }
}
