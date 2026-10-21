using Microsoft.JSInterop;

namespace BlazorBase.CRUD.SavedFilters;

/// <summary>
/// Keeps saved filters in the browser's <c>localStorage</c>, for hosts without server storage. Filters live per browser
/// profile, not per user, and do not follow the user to another device.
/// </summary>
public sealed class LocalStorageListFilterStore(IJSRuntime jsRuntime) : SnapshotListFilterStore
{
    #region Injects

    private readonly IJSRuntime JsRuntime = jsRuntime;

    #endregion

    private const string KeyPrefix = "blazorbase.listfilters:";

    protected override async Task<List<SavedListFilterInfo>> LoadAsync(string listKey, CancellationToken cancellationToken)
    {
        var json = await JsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, KeyPrefix + listKey);
        return ListFilterSerializer.DeserializeList(json);
    }

    protected override async Task PersistAsync(string listKey, List<SavedListFilterInfo> filters, CancellationToken cancellationToken) =>
        await JsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, KeyPrefix + listKey, ListFilterSerializer.SerializeList(filters));
}
