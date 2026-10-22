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
    /// list opens nothing. A list without a card configuration or card type shows a card built from its columns.
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
        && (CardType is null || BaseDialog<TModel>.DeclaresReadOnlyParameter(CardType));

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
        DismissContextMenu();

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

        if (rows.Count > 0 && IsMobileLayout)
            MultiSelectMode = true;
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
        SelectionBarHint = null;
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
    /// A key pressed on a row of the grid, reported by the grid script together with the index of the row the keyboard
    /// stands on and, for an arrow key, the row the script has already moved the keyboard to. Returns the index of the row
    /// the keyboard must end up on, or -1 when it stays where it is.
    /// </summary>
    [JSInvokable]
    public async Task<int> OnGridKeyPressedAsync(string key, bool shift, bool ctrl, int row, int target)
    {
        if (IsDisposed || ContextMenuOpen || IsCardDialogOpen)
            return -1;

        var focusRow = -1;

        await InvokeAsync(async () =>
        {
            focusRow = await HandleGridKeyAsync(key, shift, ctrl, row, target);
            StateHasChanged();
        });

        return focusRow;
    }

    /// <summary>
    /// A key pressed on a row of the grid without the row it was pressed on, so the row the keyboard last landed on is
    /// the current one.
    /// </summary>
    public async Task OnGridKeyAsync(string key, bool shift, bool ctrl)
        => await OnGridKeyPressedAsync(key, shift, ctrl, -1, -1);

    /// <summary>
    /// Applies a key to the list. <paramref name="row"/> is the row the keyboard stands on (the current row), -1 when it is
    /// not known; <paramref name="target"/> is the row an arrow key already moved the keyboard to, -1 when it did not.
    /// </summary>
    /// <returns>The index of the row the keyboard must be moved to, -1 for none.</returns>
    internal async Task<int> HandleGridKeyAsync(string key, bool shift, bool ctrl, int row = -1, int target = -1)
    {
        switch (key)
        {
            case "ArrowDown":
                return await MoveCurrentRowAsync(row, 1, shift, ctrl, target);
            case "ArrowUp":
                return await MoveCurrentRowAsync(row, -1, shift, ctrl, target);
        }

        if (ctrl)
        {
            if (string.Equals(key, "a", StringComparison.OrdinalIgnoreCase))
                await SelectAll();

            return -1;
        }

        switch (key)
        {
            case " ":
                await SelectCurrentAsync(row);
                break;
            case "Enter":
                await OpenCurrentAsync(row);
                break;
            case "Escape":
                ClearSelection();
                break;
        }

        return -1;
    }

    private int ResolveCursorIndex()
    {
        if (CursorIndex >= 0)
            return CursorIndex;

        return FocusedItem is null ? -1 : RangeCache.IndexOf(FocusedItem);
    }

    /// <summary>
    /// Whether the list picks rows one by one: the select circles are on, or two or more rows are selected. A plain arrow
    /// key then only moves the current row and leaves the selection alone.
    /// </summary>
    internal bool InMultiSelectMode => MultiSelectMode || SelectedItems.Count > 1;

    /// <summary>
    /// Moves the current row one step. Outside multi-select mode the new row replaces the selection; in multi-select mode,
    /// and with Ctrl, only the current row moves; with Shift the range from the anchor grows or shrinks.
    /// </summary>
    private async Task<int> MoveCurrentRowAsync(int row, int delta, bool extend, bool focusOnly, int target)
    {
        var total = TotalCount;

        if (total <= 0)
            return -1;

        var cursor = row >= 0 ? row : ResolveCursorIndex();
        var wanted = target >= 0 ? target : (cursor < 0 ? (delta > 0 ? 0 : total - 1) : cursor + delta);
        var destination = Math.Clamp(wanted, 0, total - 1);

        if (focusOnly || (!extend && InMultiSelectMode))
        {
            CursorIndex = destination;
            return destination;
        }

        if (extend && IsSelectionAvailable)
        {
            var anchor = AnchorIndex >= 0 ? AnchorIndex : (cursor >= 0 ? cursor : destination);
            await SelectRangeAsync(anchor, destination);
            return destination;
        }

        var rows = await FetchRowsAsync(destination, 1);

        if (rows.Count > 0)
            await SelectSingleAsync(rows[0], destination);

        return destination;
    }

    /// <summary>
    /// The row a key acts on: the row the keyboard stands on when the script reported it, otherwise the row that last
    /// had the keyboard.
    /// </summary>
    private async Task<(TModel? Item, int Index)> ResolveCurrentRowAsync(int row)
    {
        if (row >= 0)
        {
            var rows = await FetchRowsAsync(row, 1);

            if (rows.Count > 0)
            {
                CursorIndex = row;
                FocusedItem = rows[0];
                return (rows[0], row);
            }
        }

        var item = FocusedItem;

        return (item, item is null ? -1 : RangeCache.IndexOf(item));
    }

    private async Task SelectCurrentAsync(int row)
    {
        var (item, index) = await ResolveCurrentRowAsync(row);

        if (item is null)
            return;

        if (ShowsSelectionColumn)
            await ToggleAndNotifyAsync(item);
        else
            await SelectSingleAsync(item, index);
    }

    private async Task OpenCurrentAsync(int row)
    {
        var (item, index) = await ResolveCurrentRowAsync(row);

        if (item is null)
        {
            item = SelectedItems.FirstOrDefault();
            index = item is null ? -1 : RangeCache.IndexOf(item);
        }

        if (item is null)
            return;

        if (!(SelectedItems.Count == 1 && IsSelected(item)))
            await SelectSingleAsync(item, index);

        await OpenItemAsync(item);
    }

    #endregion
}
