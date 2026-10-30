using BlazorBase.CRUD.Components.Internal;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Refreshing one row of the list: after an edit only the entry that changed is read again instead of the whole
/// visible window.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    /// <summary>
    /// Reads the entry with the given id again and shows its current values in the list, without reloading the
    /// rows around it. Use it after a change the list did not make itself (a custom action, a dialog of the host).
    /// </summary>
    /// <remarks>
    /// The row keeps its position even when the new values would sort it elsewhere or filter it out; that is
    /// settled by the next reload, sort or filter change. When the entry is not held by the list right now, or cannot
    /// be read (it was deleted meanwhile, or is no longer readable), the list reloads the part that is on screen
    /// instead, as <see cref="RefreshAsync"/> does. Creating and deleting always reload.
    /// </remarks>
    /// <param name="id">The value of the entry's <c>Id</c> property.</param>
    public Task RefreshItemAsync(object id)
    {
        ArgumentNullException.ThrowIfNull(id);

        return RefreshItemCoreAsync(id, savedItem: null);
    }

    private Task RefreshEditedItemAsync(TModel savedItem)
        => GetItemId(savedItem) is { } id
            ? RefreshItemCoreAsync(id, savedItem)
            : LoadDataAsync(keepLoadedCount: true);

    /// <summary>
    /// Reads one entry with the columns the list shows and puts it in the place of its row. The saved model of the
    /// dialog stands in when the provider fails to answer; an entry the provider reports as gone, a row that is not
    /// held and a load that is under way all end in the reload of the visible window.
    /// </summary>
    private async Task RefreshItemCoreAsync(object id, TModel? savedItem)
    {
        if (IsDisposed)
            return;

        if (IsLoading)
        {
            await LoadDataAsync(keepLoadedCount: true);
            return;
        }

        TModel? fresh;

        try
        {
            fresh = await ResolvedDataProvider.GetByIdAsync(id, BuildSelectList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            fresh = savedItem;
        }

        if (IsDisposed)
            return;

        if (fresh is not null && JsModule is not null)
            await JsModule.InvokeVoidAsync("holdFocus", GridContainerRef);

        if (IsDisposed)
            return;

        if (fresh is null || !TryShowRefreshedItem(fresh))
            await LoadDataAsync(keepLoadedCount: true);
    }

    /// <summary>
    /// Replaces the held row, the selection entry and the context menu item that stand for the entry, and makes the
    /// grid or the tile view read the cache again.
    /// </summary>
    private bool TryShowRefreshedItem(TModel fresh)
    {
        if (!RangeCache.TryReplace(fresh))
            return false;

        var comparer = ItemIdentityComparer<TModel>.Instance;

        if (SelectedItems.Remove(fresh))
            SelectedItems.Add(fresh);

        if (ContextMenuItem is not null && comparer.Equals(ContextMenuItem, fresh))
            ContextMenuItem = fresh;

        DataVersion++;
        StateHasChanged();

        return true;
    }
}
