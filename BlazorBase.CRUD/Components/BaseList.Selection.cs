using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// How rows of the table are selected and opened: a click selects, Ctrl+click toggles, Shift+click selects a range,
/// the first column and a double click open the card, and the keyboard does the same without the mouse.
/// </summary>
public partial class BaseList<TModel> where TModel : class, new()
{
    internal const string OpenCellClass = "base-list-open-cell";

    #region Parameters

    /// <summary>
    /// Whether the first visible data column (or the column marked with <c>OpensItem</c>) is the open link of the row:
    /// it looks like a link and a click on it opens the card. Default true.
    /// </summary>
    [Parameter]
    public bool OpenColumn { get; set; } = true;

    /// <summary>
    /// Whether a user who may read but not edit can open a row in a read-only card. Default true; without it such a
    /// list opens nothing.
    /// </summary>
    [Parameter]
    public bool AllowView { get; set; } = true;

    /// <summary>
    /// The most rows "select all" and a Shift+click range select. Both reach rows that are not loaded yet, so a limit
    /// keeps a click from fetching a whole table; above it the selection is cut off and a message says so. Default 5000.
    /// </summary>
    [Parameter]
    public int SelectAllLimit { get; set; } = 5000;

    /// <summary>
    /// Raised when the user opens a row: a click on the open column, a double click, Enter or the Edit entry of the row
    /// menu. It is not raised for a card that is opened by a link to it.
    /// </summary>
    [Parameter]
    public EventCallback<TModel> OnItemOpened { get; set; }

    #endregion

    #region State

    private TModel? AnchorItem { get; set; }
    private int AnchorIndex { get; set; } = -1;
    private int CursorIndex { get; set; } = -1;
    private string? SelectionMessage { get; set; }

    /// <summary>
    /// Whether the select circles are shown, which is how rows are picked one by one without a modifier key. It is on
    /// while two or more rows are selected, and when entered with <see cref="EnterMultiSelectMode"/>; Escape and
    /// "Deselect all" leave it.
    /// </summary>
    internal bool MultiSelectMode { get; private set; }

    private bool ShowsSelectionColumn => IsSelectionAvailable && (MultiSelectMode || SelectedItems.Count > 1);

    private bool CanView =>
        AllowView
        && !CanEdit
        && (ClassRightsForCurrentUser & CrudRights.Read) == CrudRights.Read
        && (CardType is not null ? BaseDialog<TModel>.DeclaresReadOnlyParameter(CardType) : CardConfiguration is not null);

    private bool CanOpen => CanEdit || CanView;

    #endregion

    #region Open column

    /// <summary>
    /// The column that opens the row: the one marked <c>OpensItem</c>, otherwise the first visible column that is not the
    /// tile's action row; none when <see cref="OpenColumn"/> is off or the user can open nothing.
    /// </summary>
    private PropertyColumnConfig<TModel>? ResolveOpenColumn()
    {
        if (!OpenColumn || !CanOpen)
            return null;

        PropertyColumnConfig<TModel>? first = null;

        foreach (var column in VisibleColumnsForUser)
        {
            if (column.OpensItem)
                return column;

            if (first is null && column.MobileRole != MobileRole.Actions)
                first = column;
        }

        return first;
    }

    #endregion

    #region Mouse

    private void OnMouseDown(MouseEventArgs e)
    {
        LastShiftState = e.ShiftKey;
        LastCtrlState = e.CtrlKey || e.MetaKey;
        CellClickOpens = false;
    }

    private void OnCellClicked(FluentDataGridCell<TModel> cell)
    {
        CellClickOpens = cell.Item is not null
            && cell.Column?.Class is { } css
            && css.Contains(OpenCellClass, StringComparison.Ordinal);
    }

    private async Task OnRowClickedAsync(FluentDataGridRow<TModel> row)
    {
        var item = row.Item;
        var opensItem = CellClickOpens;
        var shift = LastShiftState;
        var ctrl = LastCtrlState;

        CellClickOpens = false;
        LastShiftState = false;
        LastCtrlState = false;

        if (item is null)
            return;

        var index = RangeCache.IndexOf(item);

        if (shift && IsSelectionAvailable && AnchorIndex >= 0 && index >= 0)
        {
            await SelectRangeAsync(AnchorIndex, index);
            return;
        }

        if (ctrl && IsSelectionAvailable)
        {
            await ToggleAndNotifyAsync(item);
            return;
        }

        await SelectSingleAsync(item, index);

        if (opensItem && !shift && !ctrl)
            await OpenItemAsync(item);
    }

    private async Task OnRowDoubleClickedAsync(FluentDataGridRow<TModel> row)
    {
        if (row.Item is not { } item)
            return;

        if (!(SelectedItems.Count == 1 && IsSelected(item)))
            await SelectSingleAsync(item, RangeCache.IndexOf(item));

        await OpenItemAsync(item);
    }

    /// <summary>
    /// A right-click on a row that is not part of the selection selects that row first, so the menu acts on what the
    /// user pointed at.
    /// </summary>
    private async Task SelectRightClickedRowAsync()
    {
        if (ContextMenuItem is not { } item || IsSelected(item))
            return;

        await SelectSingleAsync(item, RangeCache.IndexOf(item));
    }

    #endregion

    #region Opening

    /// <summary>
    /// Opens a row the way the user may: the edit card, or the read-only card when the user may only read.
    /// </summary>
    private async Task OpenItemAsync(TModel item)
    {
        if (!CanOpen)
            return;

        if (OnItemOpened.HasDelegate)
            await OnItemOpened.InvokeAsync(item);

        await OnEditClickedAsync(item);
    }

    #endregion

    #region Selection operations

    private void SetAnchor(TModel? item, int index)
    {
        AnchorItem = item;
        AnchorIndex = item is null ? -1 : index;
        CursorIndex = AnchorIndex;
    }

    private void ReplaceSelection(IEnumerable<TModel> items)
    {
        SelectionVersion++;
        SelectedItems.Clear();

        foreach (var item in items)
            SelectedItems.Add(item);
    }

    /// <summary>
    /// Makes the row the only selected one and the start of the next range.
    /// </summary>
    /// <remarks>
    /// <see cref="OnItemSelected"/> is raised when the row becomes the single selection, not again for a click on the
    /// row that already is.
    /// </remarks>
    private async Task SelectSingleAsync(TModel item, int index)
    {
        var alreadySingle = SelectedItems.Count == 1 && IsSelected(item);

        ReplaceSelection([item]);
        SetAnchor(item, index);
        SelectionMessage = null;

        if (!alreadySingle)
            await NotifySelectedAsync(item);
    }

    private async Task ToggleAndNotifyAsync(TModel item)
    {
        ToggleSelection(item);
        SelectionMessage = null;

        if (SelectedItems.Count == 1 && IsSelected(item))
            await NotifySelectedAsync(item);
    }

    private async Task NotifySelectedAsync(TModel item)
    {
        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(item);
    }

    /// <summary>
    /// Selects every row from <paramref name="anchor"/> to <paramref name="target"/> (either may come first), fetching the
    /// rows of the range that are not loaded; the anchor stays where it is so the range can be changed again.
    /// </summary>
    /// <remarks>
    /// A range longer than <see cref="SelectAllLimit"/> keeps the rows nearest the anchor and says so in a message.
    /// </remarks>
    internal async Task SelectRangeAsync(int anchor, int target)
    {
        var forward = target >= anchor;
        var span = Math.Abs(target - anchor) + 1;
        var count = Math.Min(span, Math.Max(1, SelectAllLimit));
        var start = forward ? anchor : anchor - count + 1;

        var rows = await FetchRowsAsync(start, count);

        if (rows.Count == 0)
            return;

        var anchorItem = rows[forward ? 0 : rows.Count - 1];

        ReplaceSelection(rows);
        AnchorItem = anchorItem;
        AnchorIndex = anchor;
        CursorIndex = forward ? start + rows.Count - 1 : start;
        SelectionMessage = BuildLimitMessage(count, span);
    }

    /// <summary>
    /// Selects every row that matches the current filter, up to <see cref="SelectAllLimit"/>, not only the rows that
    /// happen to be loaded.
    /// </summary>
    internal async Task SelectAll()
    {
        CloseContextMenu();

        if (!IsSelectionAvailable)
            return;

        var total = Math.Max(0, TotalCount);
        var count = Math.Min(total, Math.Max(1, SelectAllLimit));
        var rows = await FetchRowsAsync(0, count);

        ReplaceSelection(rows);
        AnchorItem = rows.Count > 0 ? rows[0] : null;
        AnchorIndex = rows.Count > 0 ? 0 : -1;
        CursorIndex = AnchorIndex;
        SelectionMessage = BuildLimitMessage(count, total);
    }

    private string? BuildLimitMessage(int selected, int wanted)
        => wanted > selected ? string.Format(FrameworkLocalizer["SelectionLimitReached"].Value, selected, wanted) : null;

    /// <summary>
    /// Reads rows by position: from the rows the list holds when they are all there, otherwise straight from the data
    /// provider in pages, because a range can be far longer than the chunks the list keeps.
    /// </summary>
    private async Task<List<TModel>> FetchRowsAsync(int start, int count)
    {
        if (count <= 0)
            return [];

        if (RangeCache.TryGetRange(start, count, out var held) && held.Count >= Math.Min(count, Math.Max(0, TotalCount - start)))
            return held;

        var rows = new List<TModel>(count);
        var pageSize = Math.Max(1, BaseQueryLimits.MaxPageSize);

        for (var skip = start; skip < start + count; skip += pageSize)
        {
            var take = Math.Min(pageSize, start + count - skip);
            var answer = await ResolvedDataProvider.GetListAsync(BuildQuery(skip, take));

            rows.AddRange(answer.Items.Take(take));

            if (answer.Items.Count < take)
                break;
        }

        return rows;
    }

    private void ClearSelection()
    {
        SelectionVersion++;
        SelectedItems.Clear();
        AnchorItem = null;
        AnchorIndex = -1;
        CursorIndex = -1;
        MultiSelectMode = false;
        SelectionMessage = null;
    }

    private void RemoveFromSelection(TModel item)
    {
        if (!SelectedItems.Remove(item))
            return;

        SelectionVersion++;

        if (AnchorItem is not null && ItemIdentityComparer<TModel>.Instance.Equals(AnchorItem, item))
        {
            AnchorItem = null;
            AnchorIndex = -1;
        }
    }

    /// <summary>
    /// Shows the select circles; the "Select more" entry of the row menu calls this.
    /// </summary>
    internal void EnterMultiSelectMode()
    {
        if (MultiSelectMode)
            return;

        MultiSelectMode = true;
        SelectionVersion++;
    }

    /// <summary>
    /// After a refresh the selected rows that are loaded again are replaced by their fresh data, so what an action
    /// reads from them is current. Rows that are not loaded stay selected as they were, and the anchor is found again.
    /// </summary>
    private void KeepSelectionAfterRefresh(ListRangeCache<TModel> cache)
    {
        if (SelectedItems.Count == 0 && !MultiSelectMode)
            return;

        foreach (var fresh in cache.ResidentItems)
        {
            if (SelectedItems.Remove(fresh))
                SelectedItems.Add(fresh);
        }

        AnchorIndex = AnchorItem is null ? -1 : cache.IndexOf(AnchorItem);

        if (AnchorIndex < 0)
            AnchorItem = null;

        CursorIndex = -1;
        SelectionVersion++;
    }

    #endregion

    #region Keyboard

    /// <summary>
    /// A key pressed on a row of the grid, reported by the grid script. Moving between the rows is done by the data grid
    /// itself; this keeps the selection with it.
    /// </summary>
    [JSInvokable]
    public async Task OnGridKeyAsync(string key, bool shift, bool ctrl)
    {
        if (IsDisposed || ContextMenuOpen || IsCardDialogOpen)
            return;

        await InvokeAsync(async () =>
        {
            await HandleGridKeyAsync(key, shift, ctrl);
            StateHasChanged();
        });
    }

    internal async Task HandleGridKeyAsync(string key, bool shift, bool ctrl)
    {
        if (ctrl)
        {
            if (string.Equals(key, "a", StringComparison.OrdinalIgnoreCase))
                await SelectAll();

            return;
        }

        switch (key)
        {
            case "ArrowDown":
                await MoveSelectionAsync(1, shift);
                break;
            case "ArrowUp":
                await MoveSelectionAsync(-1, shift);
                break;
            case " ":
                await SelectFocusedAsync();
                break;
            case "Enter":
                await OpenFocusedAsync();
                break;
            case "Escape":
                ClearSelection();
                break;
        }
    }

    private int ResolveCursorIndex()
    {
        if (CursorIndex >= 0)
            return CursorIndex;

        return FocusedItem is null ? -1 : RangeCache.IndexOf(FocusedItem);
    }

    private async Task MoveSelectionAsync(int delta, bool extend)
    {
        var total = TotalCount;

        if (total <= 0)
            return;

        var cursor = ResolveCursorIndex();
        var target = cursor < 0 ? (delta > 0 ? 0 : total - 1) : Math.Clamp(cursor + delta, 0, total - 1);

        if (extend && IsSelectionAvailable)
        {
            var anchor = AnchorIndex >= 0 ? AnchorIndex : (cursor >= 0 ? cursor : target);
            await SelectRangeAsync(anchor, target);
            return;
        }

        var rows = await FetchRowsAsync(target, 1);

        if (rows.Count > 0)
            await SelectSingleAsync(rows[0], target);
    }

    private async Task SelectFocusedAsync()
    {
        var item = FocusedItem;

        if (item is null)
            return;

        if (ShowsSelectionColumn)
            await ToggleAndNotifyAsync(item);
        else
            await SelectSingleAsync(item, RangeCache.IndexOf(item));
    }

    private async Task OpenFocusedAsync()
    {
        var item = FocusedItem ?? SelectedItems.FirstOrDefault();

        if (item is null)
            return;

        if (!(SelectedItems.Count == 1 && IsSelected(item)))
            await SelectSingleAsync(item, RangeCache.IndexOf(item));

        await OpenItemAsync(item);
    }

    #endregion
}
