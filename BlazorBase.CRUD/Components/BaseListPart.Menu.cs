using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The row menu of a list part: View or Edit, Delete, the declared item actions, then "Select more", "Select all" and
/// "Deselect all" (the order of Business Central and of <see cref="BaseList{TModel}"/>), opened by the button beside the
/// item, a right-click or Shift+F10.
/// </summary>
public partial class BaseListPart<TModel> where TModel : class, new()
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListPart.js";

    private sealed record MenuActionEntry(CrudAction<TModel> Action, CrudActionState<TModel> State, string? Reason);

    private sealed record MenuActionSection(CrudActionGroup<TModel> Group, List<MenuActionEntry> Entries);

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    private IJSObjectReference? JsModule { get; set; }
    private DotNetObjectReference<BaseListPart<TModel>>? SelfReference { get; set; }
    private ElementReference RootElement { get; set; }
    private ElementReference MenuElement { get; set; }
    private bool isDisposed;

    private TModel? MenuItem { get; set; }
    private bool MenuOpen { get; set; }
    private bool MenuPositioned { get; set; }
    private bool ShouldPositionMenu { get; set; }
    private bool ShouldFocusMenu { get; set; }
    private bool ShouldRestoreFocus { get; set; }
    private ContextMenuAnchor MenuAnchor { get; set; }
    private double MenuLeft { get; set; }
    private double MenuTop { get; set; }
    private double? MenuMaxHeight { get; set; }
    private List<MenuActionSection> MenuSections { get; set; } = [];

    private string MenuStyle
    {
        get
        {
            var style = FormattableString.Invariant($"left: {MenuLeft}px; top: {MenuTop}px;");

            return MenuMaxHeight is { } maxHeight
                ? style + FormattableString.Invariant($" max-height: {maxHeight}px;")
                : style;
        }
    }

    private bool HasRowMenu =>
        (EditMode == ListPartEditMode.Dialog && (CanEdit || CanView)) || CanDelete || IsSelectionAvailable;

    private bool MenuShowsOpen => MenuItem is not null && CanOpenItem(MenuItem);
    private bool MenuShowsSelectMore => MenuItem is not null && IsSelectionAvailable && !ShowsSelectionColumn;
    private bool MenuShowsSelectAll => IsSelectionAvailable;
    private bool MenuShowsDeselectAll => IsSelectionAvailable && (SelectedItems.Count > 0 || MultiSelectMode);
    private bool MenuHasSelectionEntries => MenuShowsSelectMore || MenuShowsSelectAll || MenuShowsDeselectAll;
    private bool MenuHasEntriesAboveSelection => MenuShowsOpen || CanDelete || MenuSections.Count > 0;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await ConnectScriptAsync();

        await PositionMenuIfJustOpenedAsync();
        await FocusMenuIfJustOpenedAsync();
        await RestoreFocusIfMenuJustClosedAsync();
        await FocusPendingRowAsync();
    }

    private async Task ConnectScriptAsync()
    {
        JsModule = await JsRuntime.InvokeAsync<IJSObjectReference>("import", InteropModulePath);

        if (isDisposed)
        {
            await JsModule.DisposeAsync();
            JsModule = null;
            return;
        }

        SelfReference = DotNetObjectReference.Create(this);
        await JsModule.InvokeVoidAsync("observe", RootElement, SelfReference);
    }

    private async Task PositionMenuIfJustOpenedAsync()
    {
        if (!ShouldPositionMenu)
            return;

        ShouldPositionMenu = false;

        var measurement = JsModule is null ? null : await JsModule.InvokeAsync<double[]?>("measure", MenuElement);

        if (measurement is { Length: >= 4 })
        {
            var placement = BaseListContextMenuPlacement.Compute(MenuAnchor, measurement[0], measurement[1], measurement[2], measurement[3]);

            MenuLeft = placement.Left;
            MenuTop = placement.Top;
            MenuMaxHeight = placement.MaxHeight;
        }

        MenuPositioned = true;
        ShouldFocusMenu = true;
        StateHasChanged();
    }

    private async Task FocusMenuIfJustOpenedAsync()
    {
        if (!ShouldFocusMenu || !MenuOpen)
            return;

        ShouldFocusMenu = false;
        await MenuElement.FocusAsync(preventScroll: true);
    }

    private async Task RestoreFocusIfMenuJustClosedAsync()
    {
        if (!ShouldRestoreFocus || JsModule is null)
            return;

        ShouldRestoreFocus = false;
        await JsModule.InvokeVoidAsync("restoreFocus");
    }

    private async Task FocusPendingRowAsync()
    {
        if (PendingFocusIndex is not { } index || JsModule is null)
            return;

        PendingFocusIndex = null;
        await JsModule.InvokeVoidAsync("focusRow", RootElement, index);
    }

    private async Task OnMenuButtonClickedAsync(TModel item, int index, MouseEventArgs e)
    {
        var anchor = JsModule is null ? null : await JsModule.InvokeAsync<double[]?>("menuButtonRect", e.ClientX, e.ClientY);

        await OpenMenuAsync(item, index, anchor is { Length: >= 4 }
            ? new ContextMenuAnchor(anchor[0], anchor[1], anchor[2], anchor[3])
            : ContextMenuAnchor.AtPoint(e.ClientX, e.ClientY));
    }

    private async Task OnRowContextMenuAsync(TModel item, int index, MouseEventArgs e)
    {
        await OpenMenuAsync(item, index, ContextMenuAnchor.AtPoint(e.ClientX, e.ClientY));
    }

    internal async Task OpenMenuForRowAsync(int rowIndex, ContextMenuAnchor anchor)
    {
        var rows = Rows;

        if (rowIndex < 0 || rowIndex >= rows.Count)
            return;

        await OpenMenuAsync(rows[rowIndex], rowIndex, anchor);
    }

    /// <summary>
    /// Opens the menu for <paramref name="item"/>. An item outside the selection is selected first, so the menu acts on
    /// what the user pointed at; an item that is part of a multi-selection keeps it.
    /// </summary>
    private async Task OpenMenuAsync(TModel item, int index, ContextMenuAnchor anchor)
    {
        if (!HasRowMenu)
            return;

        MenuItem = item;

        if (!IsSelected(item))
            await SelectSingleAsync(item, index);

        await BuildMenuSectionsAsync();

        if (JsModule is not null)
            await JsModule.InvokeVoidAsync("rememberFocus");

        MenuAnchor = anchor;
        MenuLeft = anchor.Left;
        MenuTop = anchor.Bottom;
        MenuMaxHeight = null;
        MenuPositioned = false;
        MenuOpen = true;
        ShouldPositionMenu = true;
    }

    private void CloseMenu()
    {
        MenuOpen = false;
    }

    /// <summary>
    /// Closes the menu without acting on it, and puts the keyboard back on the control it came from.
    /// </summary>
    private void DismissMenu()
    {
        if (!MenuOpen)
            return;

        MenuOpen = false;
        ShouldRestoreFocus = true;
    }

    private void OnMenuKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            DismissMenu();
    }

    private bool IsMenuOpenFor(TModel item) =>
        MenuOpen && MenuItem is not null && ItemIdentityComparer<TModel>.Instance.Equals(MenuItem, item);

    /// <summary>
    /// Works out which declared item actions the menu offers for the clicked item, whether each is enabled and why not.
    /// Done once when the menu opens, so the async <c>Visible</c> predicates run for the clicked item only.
    /// </summary>
    private async Task BuildMenuSectionsAsync()
    {
        var sections = new List<MenuActionSection>();
        var item = MenuItem;

        if (item is not null && ActionGroups is not null)
        {
            IReadOnlyList<TModel> clicked = [item];

            foreach (var group in ActionGroups.OrderBy(g => g.Order))
            {
                if (!await CrudActionEvaluator<TModel>.IsGroupVisibleAsync(group, CrudActionContext.ListPart, CurrentUser, item, clicked))
                    continue;

                var entries = new List<MenuActionEntry>();

                foreach (var action in group.Actions.Where(a => a.Scope != CrudActionScope.None).OrderBy(a => a.Order))
                {
                    var targets = CrudActionEvaluator<TModel>.ResolveRowMenuTargets(action, item, SelectedItems, IsSelected);

                    if (!await CrudActionEvaluator<TModel>.IsVisibleAsync(action, CrudActionContext.ListPart, CurrentUser, item, targets))
                        continue;

                    var state = CrudActionEvaluator<TModel>.Evaluate(action, targets);
                    var reason = CrudActionEvaluator<TModel>.DescribeBlock(action, state, key => FrameworkLocalizer[key].Value, text => ResolveActionText(text));

                    entries.Add(new MenuActionEntry(action, state, reason));
                }

                if (entries.Count > 0)
                    sections.Add(new MenuActionSection(group, entries));
            }
        }

        MenuSections = sections;
    }

    private string ResolveActionText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return Localizer is not null ? Localizer[text]?.Value ?? text : text;
    }

    private async Task OnMenuOpenAsync()
    {
        var item = MenuItem;
        CloseMenu();

        if (item is null)
            return;

        await OpenItemAsync(item);
    }

    private async Task OnMenuDeleteAsync()
    {
        var item = MenuItem;
        CloseMenu();

        if (item is null || !CanDelete)
            return;

        if (SelectedItems.Count > 1 && IsSelected(item))
        {
            foreach (var selected in Rows.Where(IsSelected).ToList())
                await OnRemoveItemAsync(selected);
        }
        else
        {
            await OnRemoveItemAsync(item);
        }
    }

    private void OnMenuSelectMore()
    {
        if (MenuItem is { } item && !IsSelected(item))
            SelectedItems.Add(item);

        EnterMultiSelectMode();
        DismissMenu();
    }

    private async Task OnMenuActionClickedAsync(MenuActionEntry entry)
    {
        var item = MenuItem;
        CloseMenu();

        if (entry.Action.Action is null || !entry.State.IsEnabled)
            return;

        var eventArgs = CrudActionEvaluator<TModel>.BuildEventArgs(
            entry.Action, entry.State.Targets, item, ServiceProvider, CrudActionContext.ListPart);

        await entry.Action.Action(eventArgs);
        PruneSelection();
    }

    public async ValueTask DisposeAsync()
    {
        isDisposed = true;

        if (JsModule is null)
            return;

        try
        {
            await JsModule.InvokeVoidAsync("unobserve", RootElement);
            await JsModule.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or ObjectDisposedException or InvalidOperationException)
        {
        }

        SelfReference?.Dispose();
    }
}
