using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// How the items of a list part are selected and opened, the same model as the rows of <see cref="BaseList{TModel}"/>:
/// a click selects, Ctrl+click toggles, Shift+click selects a range, the item label and a double click open the item,
/// and the keyboard does the same without the mouse. The items are all in memory, so a range is plain positions.
/// </summary>
public partial class BaseListPart<TModel> where TModel : class, new()
{
    #region Parameters

    /// <summary>
    /// Shows the part for viewing only: nothing can be added, removed, reordered or edited, and an item opens in a
    /// read-only card. <c>BaseCard</c> sets it from its own <c>ReadOnly</c>.
    /// </summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Raised when an item becomes the single selection, not again for a click on the item that already is.
    /// </summary>
    [Parameter]
    public EventCallback<TModel> OnItemSelected { get; set; }

    /// <summary>
    /// Raised when the user opens an item: a click on its label, a double click, Enter or the Edit or View entry of the
    /// row menu.
    /// </summary>
    [Parameter]
    public EventCallback<TModel> OnItemOpened { get; set; }

    #endregion

    #region State

    private HashSet<TModel> SelectedItems { get; } = new(ItemIdentityComparer<TModel>.Instance);
    private HashSet<TModel> BlockedItems { get; set; } = new(ItemIdentityComparer<TModel>.Instance);
    private ICollection<TModel>? PreviousItems { get; set; }
    private int AnchorIndex { get; set; } = -1;
    private int? PendingFocusIndex { get; set; }

    /// <summary>
    /// Whether the select circles are shown, which is how items are picked one by one without a modifier key. It is on
    /// while two or more items are selected, and after "Select more"; Escape and "Deselect all" leave it.
    /// </summary>
    internal bool MultiSelectMode { get; private set; }

    private bool UsesInlineFields => EditMode == ListPartEditMode.Inline && CardConfiguration?.Fields is { Count: > 0 };

    private IReadOnlyList<TModel> Rows => Items is null ? [] : Items as IReadOnlyList<TModel> ?? [.. Items];

    /// <summary>
    /// Whether selecting items leads anywhere: only a delete or a declared item action consumes a selection.
    /// </summary>
    private bool IsSelectionAvailable => CanDelete || HasItemActions;

    private bool ShowsSelectionColumn => IsSelectionAvailable && (MultiSelectMode || SelectedItems.Count > 1);

    private bool HasItemActions =>
        ActionGroups is not null
        && ActionGroups.Any(group => group.Actions.Any(action => action.Scope != CrudActionScope.None && action.Contexts.HasFlag(CrudActionContext.ListPart)));

    private bool CanEdit => !ReadOnly && (ClassRightsForCurrentUser & CrudRights.Modify) == CrudRights.Modify;

    private bool CanEditItem(TModel item) => !ReadOnly && (PendingAdds.Contains(item) || CanEdit);

    private bool CanView =>
        (ClassRightsForCurrentUser & CrudRights.Read) == CrudRights.Read
        && (CardType is null || BaseDialog<TModel>.DeclaresReadOnlyParameter(CardType));

    /// <summary>
    /// Whether the item label opens the item: only in dialog mode, as the edit card or, without edit rights, the read-only card.
    /// </summary>
    private bool CanOpenItem(TModel item) => EditMode == ListPartEditMode.Dialog && (CanEditItem(item) || CanView);

    private bool IsSelected(TModel item) => SelectedItems.Contains(item);

    private string ResolveRowClass(TModel item, bool selected)
    {
        var css = "base-listpart-item";

        if (selected)
            css += " base-listpart-item-selected";

        if (BlockedItems.Contains(item))
            css += " base-listpart-item-blocked";

        if (IsMenuOpenFor(item))
            css += " base-listpart-item-menu-open";

        return css;
    }

    #endregion

    #region Lifetime

    /// <summary>
    /// A different collection clears the selection; the same collection edited in place (items added or removed) keeps it
    /// for the items that are still there.
    /// </summary>
    private void SyncSelectionWithItems()
    {
        if (PreviousItems is not null && !ReferenceEquals(PreviousItems, Items))
            ClearSelection();
        else
            PruneSelection();

        PreviousItems = Items;
    }

    private void PruneSelection()
    {
        if (SelectedItems.Count == 0 && BlockedItems.Count == 0)
            return;

        var present = new HashSet<TModel>(Rows, ItemIdentityComparer<TModel>.Instance);

        SelectedItems.RemoveWhere(item => !present.Contains(item));
        BlockedItems.RemoveWhere(item => !present.Contains(item));

        if (AnchorIndex >= present.Count)
            AnchorIndex = -1;
    }

    #endregion

    #region Mouse

    private async Task OnRowClickedAsync(TModel item, int index, MouseEventArgs e, bool opensItem)
    {
        var shift = e.ShiftKey;
        var ctrl = e.CtrlKey || e.MetaKey;

        if (shift && IsSelectionAvailable && AnchorIndex >= 0)
        {
            SelectRange(AnchorIndex, index);
            return;
        }

        if (ctrl && IsSelectionAvailable)
        {
            await ToggleAndNotifyAsync(item, index);
            return;
        }

        await SelectSingleAsync(item, index);

        if (opensItem && !shift && !ctrl)
            await OpenItemAsync(item);
    }

    private async Task OnRowDoubleClickedAsync(TModel item, int index)
    {
        if (!CanOpenItem(item))
            return;

        if (!(SelectedItems.Count == 1 && IsSelected(item)))
            await SelectSingleAsync(item, index);

        await OpenItemAsync(item);
    }

    #endregion

    #region Opening

    /// <summary>
    /// Opens an item the way the user may: the edit card, or the read-only card when the user may only read.
    /// </summary>
    private async Task OpenItemAsync(TModel item)
    {
        if (!CanOpenItem(item))
            return;

        if (OnItemOpened.HasDelegate)
            await OnItemOpened.InvokeAsync(item);

        await OpenEditDialogAsync(item);
    }

    #endregion

    #region Selection operations

    private void SetAnchor(int index) => AnchorIndex = index;

    private void ReplaceSelection(IEnumerable<TModel> items)
    {
        SelectedItems.Clear();

        foreach (var item in items)
            SelectedItems.Add(item);
    }

    /// <summary>
    /// Makes the item the only selected one and the start of the next range.
    /// </summary>
    private async Task SelectSingleAsync(TModel item, int index)
    {
        var alreadySingle = SelectedItems.Count == 1 && IsSelected(item);

        ReplaceSelection([item]);
        SetAnchor(index);

        if (!alreadySingle && OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(item);
    }

    private async Task ToggleAndNotifyAsync(TModel item, int index)
    {
        if (!SelectedItems.Remove(item))
            SelectedItems.Add(item);

        SetAnchor(index);

        if (SelectedItems.Count == 1 && IsSelected(item) && OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(item);
    }

    /// <summary>
    /// Selects every item from <paramref name="anchor"/> to <paramref name="target"/> (either may come first); the anchor
    /// stays where it is so the range can be changed again.
    /// </summary>
    internal void SelectRange(int anchor, int target)
    {
        var rows = Rows;

        if (rows.Count == 0)
            return;

        var from = Math.Clamp(Math.Min(anchor, target), 0, rows.Count - 1);
        var to = Math.Clamp(Math.Max(anchor, target), 0, rows.Count - 1);

        ReplaceSelection(rows.Skip(from).Take(to - from + 1));
        AnchorIndex = Math.Clamp(anchor, 0, rows.Count - 1);
    }

    internal void SelectAll()
    {
        DismissMenu();

        if (!IsSelectionAvailable)
            return;

        var rows = Rows;

        ReplaceSelection(rows);
        AnchorIndex = rows.Count > 0 ? 0 : -1;
    }

    private void ClearSelection()
    {
        SelectedItems.Clear();
        BlockedItems.Clear();
        AnchorIndex = -1;
        MultiSelectMode = false;
    }

    private void DeselectAll()
    {
        ClearSelection();
        DismissMenu();
    }

    /// <summary>
    /// Shows the select circles; the "Select more" entry of the row menu calls this.
    /// </summary>
    internal void EnterMultiSelectMode() => MultiSelectMode = true;

    private void RemoveFromSelection(TModel item)
    {
        SelectedItems.Remove(item);
        BlockedItems.Remove(item);
    }

    private void OnBlockedItemsChanged(IReadOnlyCollection<TModel> items)
    {
        BlockedItems = new HashSet<TModel>(items, ItemIdentityComparer<TModel>.Instance);
    }

    private void OnToolbarActionExecuted() => PruneSelection();

    #endregion

    #region Keyboard

    /// <summary>
    /// A key pressed on one of the part's own controls, reported by the part script together with the position of its item.
    /// </summary>
    [JSInvokable]
    public async Task OnKeyAsync(string key, bool shift, bool ctrl, int rowIndex)
    {
        if (isDisposed || MenuOpen)
            return;

        await InvokeAsync(async () =>
        {
            await HandleKeyAsync(key, shift, ctrl, rowIndex);
            StateHasChanged();
        });
    }

    /// <summary>
    /// Opens the row menu for the item whose control has the keyboard (Shift+F10 or the context-menu key), beside that
    /// item's button; the part script reports the key together with the box to anchor to.
    /// </summary>
    [JSInvokable]
    public async Task OnContextMenuKeyAsync(int rowIndex, double left, double top, double right, double bottom)
    {
        if (isDisposed || MenuOpen)
            return;

        await InvokeAsync(async () =>
        {
            await OpenMenuForRowAsync(rowIndex, new ContextMenuAnchor(left, top, right, bottom));
            StateHasChanged();
        });
    }

    /// <summary>
    /// Escape, reported by the part script before the card can react to it: closes the open row menu, otherwise clears the
    /// selection.
    /// </summary>
    [JSInvokable]
    public async Task OnEscapeAsync()
    {
        if (isDisposed)
            return;

        await InvokeAsync(() =>
        {
            if (MenuOpen)
                DismissMenu();
            else
                ClearSelection();

            StateHasChanged();
        });
    }

    internal async Task HandleKeyAsync(string key, bool shift, bool ctrl, int rowIndex)
    {
        switch (key)
        {
            case "ArrowDown":
                await MoveSelectionAsync(rowIndex, 1, shift, ctrl);
                return;
            case "ArrowUp":
                await MoveSelectionAsync(rowIndex, -1, shift, ctrl);
                return;
        }

        if (ctrl)
        {
            if (string.Equals(key, "a", StringComparison.OrdinalIgnoreCase))
                SelectAll();

            return;
        }

        switch (key)
        {
            case " ":
                await SelectFocusedAsync(rowIndex);
                break;
            case "Enter":
                await OpenFocusedAsync(rowIndex);
                break;
            case "Escape":
                ClearSelection();
                break;
        }
    }

    /// <summary>
    /// Whether the part picks items one by one: the select circles are on, or two or more items are selected. A plain arrow
    /// key then only moves the current item and leaves the selection alone.
    /// </summary>
    internal bool InMultiSelectMode => MultiSelectMode || SelectedItems.Count > 1;

    /// <summary>
    /// Moves the current item one step. Outside multi-select mode the new item replaces the selection; in multi-select mode,
    /// and with Ctrl, only the current item (the keyboard) moves; with Shift the range from the anchor grows or shrinks.
    /// </summary>
    private async Task MoveSelectionAsync(int cursor, int delta, bool extend, bool focusOnly)
    {
        var rows = Rows;

        if (rows.Count == 0)
            return;

        var target = cursor < 0 ? (delta > 0 ? 0 : rows.Count - 1) : Math.Clamp(cursor + delta, 0, rows.Count - 1);

        var movesOnlyTheKeyboard = focusOnly || (!extend && InMultiSelectMode);

        if (!movesOnlyTheKeyboard && extend && IsSelectionAvailable)
            SelectRange(AnchorIndex >= 0 ? AnchorIndex : (cursor >= 0 ? cursor : target), target);
        else if (!movesOnlyTheKeyboard)
            await SelectSingleAsync(rows[target], target);

        PendingFocusIndex = target;
    }

    private async Task SelectFocusedAsync(int index)
    {
        var rows = Rows;

        if (index < 0 || index >= rows.Count)
            return;

        if (ShowsSelectionColumn)
            await ToggleAndNotifyAsync(rows[index], index);
        else
            await SelectSingleAsync(rows[index], index);
    }

    private async Task OpenFocusedAsync(int index)
    {
        var rows = Rows;

        if (index < 0 || index >= rows.Count)
            return;

        var item = rows[index];

        if (!CanOpenItem(item))
            return;

        if (!(SelectedItems.Count == 1 && IsSelected(item)))
            await SelectSingleAsync(item, index);

        await OpenItemAsync(item);
    }

    #endregion
}
