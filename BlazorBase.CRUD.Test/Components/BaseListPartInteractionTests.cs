using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A list part inside a card follows the interaction model of the main list: a click selects an item, Ctrl+click toggles,
/// Shift+click selects a range, the label and a double click open the item (read-only when the part or the card is), the
/// three-dot button opens the row menu in the Business Central order, and the toolbar actions are enabled by the selection.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListPartInteractionTests : BunitTestContextBase
{
    private const string RowSelector = ".base-listpart-item";
    private const string LabelSelector = ".base-listpart-item-content";
    private const string MenuButtonSelector = ".base-listpart-row-menu";
    private const string Divider = "-";

    private readonly List<BaseDialogData<TestProduct>> openedItems = [];
    private readonly List<BaseDialogData<TestReview>> openedReviews = [];
    private readonly TestProduct alpha = new() { Id = Guid.NewGuid(), Name = "Alpha" };
    private readonly TestProduct beta = new() { Id = Guid.NewGuid(), Name = "Beta" };
    private readonly TestProduct gamma = new() { Id = Guid.NewGuid(), Name = "Gamma" };
    private readonly TestProduct delta = new() { Id = Guid.NewGuid(), Name = "Delta" };

    public BaseListPartInteractionTests() => Services.AddLocalization();

    private string L(string key) => Framework[key].Value;

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    #region Selecting

    [Fact]
    public void Click_SelectsTheItemAndReplacesTheSelection()
    {
        var cut = RenderPart();

        cut.FindAll(RowSelector)[0].Click();
        Assert.Equal(["Alpha"], SelectedNames(cut));

        cut.FindAll(RowSelector)[2].Click();
        Assert.Equal(["Gamma"], SelectedNames(cut));
        Assert.Equal("true", cut.FindAll(RowSelector)[2].GetAttribute("aria-current"));
        Assert.Empty(openedItems);
    }

    [Fact]
    public void CtrlClick_ToggleItemsInAndOutOfTheSelection()
    {
        var cut = RenderPart();

        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[2].Click(new MouseEventArgs { CtrlKey = true });
        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));

        cut.FindAll(RowSelector)[0].Click(new MouseEventArgs { MetaKey = true });
        Assert.Equal(["Gamma"], SelectedNames(cut));
    }

    [Fact]
    public void ShiftClick_SelectsTheRangeFromTheAnchor_AndKeepsTheAnchorForTheNextRange()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);

        cut.FindAll(RowSelector)[1].Click();
        cut.FindAll(RowSelector)[3].Click(new MouseEventArgs { ShiftKey = true });
        Assert.Equal(["Beta", "Gamma", "Delta"], SelectedNames(cut));

        cut.FindAll(RowSelector)[0].Click(new MouseEventArgs { ShiftKey = true });
        Assert.Equal(["Alpha", "Beta"], SelectedNames(cut));
    }

    [Fact]
    public void TwoSelectedItems_ShowTheSelectCircles_AndTheCircleTogglesAnItem()
    {
        var cut = RenderPart();
        Assert.Empty(cut.FindAll(".select-circle"));

        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[1].Click(new MouseEventArgs { CtrlKey = true });

        Assert.Equal(3, cut.FindAll(".select-circle").Count);
        Assert.Contains(L("DeselectAll"), cut.Find(".base-listpart-selection-info").TextContent);

        cut.FindAll(".select-circle")[1].Click();

        Assert.Equal(["Alpha"], SelectedNames(cut));
    }

    [Fact]
    public void OnItemSelected_IsRaisedWhenAnItemBecomesTheSingleSelection_NotAgainForTheSameItem()
    {
        var selected = new List<string>();
        var cut = RenderPart(configure: parameters => parameters
            .Add(p => p.OnItemSelected, item => selected.Add(item.Name)));

        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[1].Click();

        Assert.Equal(["Alpha", "Beta"], selected);
    }

    [Fact]
    public void ReplacingTheItems_ClearsTheSelection_AddingInPlaceKeepsIt()
    {
        var items = new List<TestProduct> { alpha, beta };
        var cut = RenderPart(items);
        cut.FindAll(RowSelector)[1].Click();

        items.Add(gamma);
        cut.Render(parameters => parameters.Add(p => p.Items, items));
        Assert.Equal(["Beta"], SelectedNames(cut));
        Assert.Equal(3, cut.FindAll(RowSelector).Count);

        cut.Render(parameters => parameters.Add(p => p.Items, new List<TestProduct> { alpha, beta, gamma }));
        Assert.Empty(SelectedNames(cut));
    }

    #endregion

    #region Opening

    [Fact]
    public void ClickingTheLabel_SelectsTheItemAndOpensItInTheDialog()
    {
        var opened = new List<string>();
        var cut = RenderPart(configure: parameters => parameters.Add(p => p.OnItemOpened, item => opened.Add(item.Name)));

        cut.FindAll(LabelSelector)[1].Click();

        var card = Assert.Single(openedItems);
        Assert.Same(beta, card.Model);
        Assert.False(card.ReadOnly);
        Assert.Equal(["Beta"], SelectedNames(cut));
        Assert.Equal(["Beta"], opened);
        Assert.Equal("button", cut.FindAll(LabelSelector)[1].GetAttribute("role"));
    }

    [Fact]
    public void ClickingTheLabelWithCtrl_TogglesInsteadOfOpening()
    {
        var cut = RenderPart();

        cut.FindAll(LabelSelector)[0].Click();
        openedItems.Clear();
        cut.FindAll(LabelSelector)[1].Click(new MouseEventArgs { CtrlKey = true });

        Assert.Empty(openedItems);
        Assert.Equal(["Alpha", "Beta"], SelectedNames(cut));
    }

    [Fact]
    public void ClickingTheRowOutsideTheLabel_OnlySelects()
    {
        var cut = RenderPart();

        cut.FindAll(RowSelector)[1].Click();

        Assert.Empty(openedItems);
        Assert.Equal(["Beta"], SelectedNames(cut));
    }

    [Fact]
    public void DoubleClickingTheRow_OpensTheItem()
    {
        var cut = RenderPart();

        cut.FindAll(RowSelector)[2].DoubleClick();

        Assert.Same(gamma, Assert.Single(openedItems).Model);
        Assert.Equal(["Gamma"], SelectedNames(cut));
    }

    [Fact]
    public void AReadOnlyPart_OpensTheItemReadOnly_AndOffersNeitherAddNorDelete()
    {
        var cut = RenderPart(configure: parameters => parameters.Add(p => p.ReadOnly, true));

        Assert.Empty(cut.FindAll(".base-listpart-header fluent-button"));

        cut.FindAll(LabelSelector)[0].Click();
        Assert.True(Assert.Single(openedItems).ReadOnly);

        OpenMenu(cut, 1);
        Assert.Equal([L("View")], MenuTokens(cut));
    }

    [Fact]
    public void AUserWithoutModifyRight_OpensTheItemReadOnly()
    {
        var cut = RenderPart(role: "Manager");

        Assert.Empty(cut.FindAll(".base-listpart-header fluent-button"));

        cut.FindAll(LabelSelector)[0].Click();
        Assert.True(Assert.Single(openedItems).ReadOnly);

        OpenMenu(cut, 0);
        Assert.Equal([L("View")], MenuTokens(cut));
    }

    [Fact]
    public void AReadOnlyCard_ReachesItsListPart_NoAddNoDeleteAndAReadOnlyItemDialog()
    {
        this.AddAuthorization().SetAuthorized("admin");
        var dialogService = Substitute.For<IDialogService>();
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult(DialogResult.Cancel()));
        dialogService.ShowDialogAsync<BaseListPartItemDialog<TestReview>>(Arg.Any<object>(), Arg.Any<DialogParameters>())
            .Returns(call =>
            {
                openedReviews.Add((BaseDialogData<TestReview>)call[0]);
                return Task.FromResult(reference);
            });
        Services.AddSingleton(dialogService);

        var product = new TestProduct { Id = Guid.NewGuid(), Name = "Product", Reviews = [new TestReview { Id = Guid.NewGuid() }] };
        var cut = Render<BaseCard<TestProduct>>(parameters => parameters
            .Add(p => p.Model, product)
            .Add(p => p.IsNew, false)
            .Add(p => p.ReadOnly, true)
            .Add(p => p.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<ListPartField<TestProduct>>(0);
                builder.AddAttribute(1, nameof(ListPartField<TestProduct>.Property), (System.Linq.Expressions.Expression<Func<TestProduct, object?>>)(p => p.Reviews));
                builder.CloseComponent();
            })));

        Assert.Empty(cut.FindAll(".base-listpart-header fluent-button"));

        cut.Find(LabelSelector).Click();

        Assert.True(Assert.Single(openedReviews).ReadOnly);
        OpenMenu(cut, 0);
        Assert.Equal([L("View")], MenuTokens(cut));
    }

    #endregion

    #region Row menu

    [Fact]
    public void RowMenu_FollowsTheBusinessCentralOrder()
    {
        var cut = RenderPart(groups: [Group(Action("Run", CrudActionScope.Single))]);

        OpenMenu(cut, 1);

        Assert.Equal(
            [L("Edit"), L("Delete"), "Run", Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            MenuTokens(cut));
    }

    [Fact]
    public void RowMenu_OnAnItemOutsideTheSelection_SelectsThatItemFirst()
    {
        var cut = RenderPart();
        cut.FindAll(RowSelector)[0].Click();

        OpenMenu(cut, 2);

        Assert.Equal(["Gamma"], SelectedNames(cut));
        Assert.Contains("base-listpart-item-menu-open", cut.FindAll(RowSelector)[2].ClassName);
    }

    [Fact]
    public void RowMenu_OnAnItemOfTheMultiSelection_KeepsTheSelection()
    {
        var cut = RenderPart();
        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[2].Click(new MouseEventArgs { CtrlKey = true });

        OpenMenu(cut, 2);

        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));
        Assert.Contains(string.Format(L("DeleteCount"), 2), MenuTokens(cut));
    }

    [Fact]
    public void RowMenu_SelectMore_ShowsTheSelectCirclesAndKeepsTheItemSelected()
    {
        var cut = RenderPart();

        OpenMenu(cut, 1);
        MenuItem(cut, L("SelectMore")).Click();

        Assert.Equal(["Beta"], SelectedNames(cut));
        Assert.Equal(3, cut.FindAll(".select-circle").Count);
        Assert.Empty(cut.FindAll(".context-menu"));
    }

    [Fact]
    public void RowMenu_SelectAll_AndDeselectAll()
    {
        var cut = RenderPart();

        OpenMenu(cut, 0);
        MenuItem(cut, L("SelectAll")).Click();
        Assert.Equal(["Alpha", "Beta", "Gamma"], SelectedNames(cut));

        OpenMenu(cut, 0);
        MenuItem(cut, L("DeselectAll")).Click();
        Assert.Empty(SelectedNames(cut));
        Assert.Empty(cut.FindAll(".select-circle"));
    }

    [Fact]
    public void RowMenu_Delete_RemovesTheItemAndRaisesTheItemEvents()
    {
        var before = new List<string>();
        var after = new List<string>();
        var items = new List<TestProduct> { alpha, beta, gamma };
        var cut = RenderPart(items, configure: parameters => parameters
            .Add(p => p.OnBeforeItemRemoved, args => before.Add(args.Item.Name))
            .Add(p => p.OnAfterItemRemoved, item => after.Add(item.Name)));

        OpenMenu(cut, 1);
        MenuItem(cut, L("Delete")).Click();

        Assert.Equal(["Alpha", "Gamma"], items.Select(item => item.Name));
        Assert.Equal(["Beta"], before);
        Assert.Equal(["Beta"], after);
        Assert.Equal(2, cut.FindAll(RowSelector).Count);
        Assert.Empty(SelectedNames(cut));
        Assert.True(cut.Instance.HasPendingChanges);
    }

    [Fact]
    public void RowMenu_Delete_HonoursACancelledBeforeEvent()
    {
        var items = new List<TestProduct> { alpha, beta };
        var cut = RenderPart(items, configure: parameters => parameters
            .Add(p => p.OnBeforeItemRemoved, args => args.Cancel = true));

        OpenMenu(cut, 0);
        MenuItem(cut, L("Delete")).Click();

        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void RowMenu_Delete_OnAMultiSelection_RemovesEverySelectedItemWithItsEvents()
    {
        var before = new List<string>();
        var after = new List<string>();
        var items = new List<TestProduct> { alpha, beta, gamma, delta };
        var cut = RenderPart(items, configure: parameters => parameters
            .Add(p => p.OnBeforeItemRemoved, args =>
            {
                before.Add(args.Item.Name);
                args.Cancel = args.Item.Name == "Gamma";
            })
            .Add(p => p.OnAfterItemRemoved, item => after.Add(item.Name)));

        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[1].Click(new MouseEventArgs { CtrlKey = true });
        cut.FindAll(RowSelector)[2].Click(new MouseEventArgs { CtrlKey = true });
        OpenMenu(cut, 1);
        Assert.Contains(string.Format(L("DeleteCount"), 3), MenuTokens(cut));
        MenuItem(cut, string.Format(L("DeleteCount"), 3)).Click();

        Assert.Equal(["Alpha", "Beta", "Gamma"], before);
        Assert.Equal(["Alpha", "Beta"], after);
        Assert.Equal(["Gamma", "Delta"], items.Select(item => item.Name));
        Assert.Equal(["Gamma"], SelectedNames(cut));
    }

    [Fact]
    public void RowMenu_DeclaredAction_RunsOnTheClickedItem()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var action = Action("Run", CrudActionScope.Single, run: args => received = args);
        var cut = RenderPart(groups: [Group(action)]);

        OpenMenu(cut, 2);
        MenuItem(cut, "Run").Click();

        Assert.NotNull(received);
        Assert.Same(gamma, received.Item);
        Assert.Equal(CrudActionContext.ListPart, received.Context);
    }

    [Fact]
    public void RowMenu_MultipleAction_RunsOnTheWholeSelectionWhenTheClickedItemIsPartOfIt()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var action = Action("Run", CrudActionScope.Multiple, run: args => received = args);
        var cut = RenderPart(groups: [Group(action)]);

        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[1].Click(new MouseEventArgs { CtrlKey = true });
        OpenMenu(cut, 0);
        MenuItem(cut, "Run").Click();

        Assert.NotNull(received);
        Assert.Equal(["Alpha", "Beta"], received.SelectedItems.Select(item => item.Name));
    }

    [Fact]
    public void RowMenu_ActionWithoutScope_IsNotOffered()
    {
        var cut = RenderPart(groups: [Group(Action("Import", CrudActionScope.None))]);

        OpenMenu(cut, 0);

        Assert.DoesNotContain("Import", MenuTokens(cut));
    }

    [Fact]
    public void RowMenu_DisabledAction_IsGreyedOutWithItsReason()
    {
        var action = Action("Run", CrudActionScope.Single, enabled: item => item.Name != "Beta");
        action.DisabledReason = "Beta is locked";
        var cut = RenderPart(groups: [Group(action)]);

        OpenMenu(cut, 1);

        var entry = MenuItem(cut, "Run");
        Assert.Equal("true", entry.GetAttribute("aria-disabled"));
        Assert.Contains("Beta is locked", entry.GetAttribute("title"));
    }

    [Fact]
    public void RowMenu_OpensForTheKeyboard_BesideTheItemsButton()
    {
        var cut = RenderPart();

        cut.InvokeAsync(() => cut.Instance.OnContextMenuKeyAsync(1, 10, 20, 42, 52)).GetAwaiter().GetResult();

        Assert.NotEmpty(cut.FindAll(".context-menu"));
        Assert.Equal(["Beta"], SelectedNames(cut));
    }

    [Fact]
    public void RightClickOnAnItem_SelectsItAndOpensTheMenu()
    {
        var cut = RenderPart();

        cut.FindAll(RowSelector)[1].ContextMenu();

        Assert.NotEmpty(cut.FindAll(".context-menu"));
        Assert.Equal(["Beta"], SelectedNames(cut));
    }

    #endregion

    #region Toolbar

    [Fact]
    public void Toolbar_SingleAction_IsEnabledOnlyWithExactlyOneSelectedItem_AndSaysWhy()
    {
        var cut = RenderPart(groups: [Group(Action("Run", CrudActionScope.Single))]);

        Assert.True(IsToolbarDisabled(cut));
        Assert.Equal(L("ActionSelectRowFirst"), ToolbarReason(cut));

        cut.FindAll(RowSelector)[0].Click();
        Assert.False(IsToolbarDisabled(cut));

        cut.FindAll(RowSelector)[1].Click(new MouseEventArgs { CtrlKey = true });
        Assert.True(IsToolbarDisabled(cut));
        Assert.Equal(L("ActionSelectExactlyOne"), ToolbarReason(cut));
    }

    [Fact]
    public void Toolbar_MultipleAction_NamesTheBlockingRows_AndMarksThemWhileThePointerIsOnTheButton()
    {
        var cut = RenderPart(groups: [Group(Action("Run", CrudActionScope.Multiple, enabled: item => item.Name != "Beta"))]);

        Assert.True(IsToolbarDisabled(cut));
        cut.FindAll(RowSelector)[0].Click();
        Assert.False(IsToolbarDisabled(cut));

        cut.FindAll(RowSelector)[1].Click(new MouseEventArgs { CtrlKey = true });
        cut.FindAll(RowSelector)[2].Click(new MouseEventArgs { CtrlKey = true });

        Assert.True(IsToolbarDisabled(cut));
        Assert.Equal(string.Format(L("ActionNotPossibleForRows"), 1, 3), ToolbarReason(cut));
        Assert.Empty(cut.FindAll(".base-listpart-item-blocked"));

        cut.Find(".base-action-toolbar-item").MouseEnter();

        var blocked = cut.FindAll(".base-listpart-item-blocked");
        Assert.Single(blocked);
        Assert.Equal("Beta", blocked[0].QuerySelector(LabelSelector)!.TextContent.Trim());

        cut.Find(".base-action-toolbar-item").MouseLeave();
        Assert.Empty(cut.FindAll(".base-listpart-item-blocked"));
    }

    [Fact]
    public void Toolbar_ActionWithoutScope_IsAlwaysEnabled()
    {
        var cut = RenderPart(groups: [Group(Action("Import", CrudActionScope.None))]);

        Assert.False(IsToolbarDisabled(cut));
    }

    #endregion

    #region Keyboard

    [Fact]
    public async Task ArrowKeys_MoveTheSingleSelection_AndShiftExtendsIt()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);
        cut.FindAll(RowSelector)[0].Click();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 0));
        Assert.Equal(["Beta"], SelectedNames(cut));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", true, false, 1));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", true, false, 2));
        Assert.Equal(["Beta", "Gamma", "Delta"], SelectedNames(cut));
    }

    [Fact]
    public async Task ArrowKeys_InMultiSelectMode_OnlyMoveTheCurrentItem_AndTheSelectionStays()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);
        cut.FindAll(RowSelector)[0].Click();
        cut.FindAll(RowSelector)[2].Click(new MouseEventArgs { CtrlKey = true });
        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 2));

        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));
        Assert.Equal(3, FocusedRowIndex());

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowUp", false, false, 3));

        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));
        Assert.Equal(2, FocusedRowIndex());

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, 3));
        Assert.Equal(["Alpha", "Gamma", "Delta"], SelectedNames(cut));
    }

    [Fact]
    public async Task ArrowKeys_AfterSelectMore_OnlyMoveTheCurrentItem()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);
        cut.FindAll(RowSelector)[1].Click();
        await cut.InvokeAsync(() => { cut.Instance.EnterMultiSelectMode(); return Task.CompletedTask; });

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 1));

        Assert.Equal(["Beta"], SelectedNames(cut));
        Assert.Equal(2, FocusedRowIndex());
    }

    [Fact]
    public async Task CtrlArrow_AlwaysMovesOnlyTheCurrentItem()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);
        cut.FindAll(RowSelector)[0].Click();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, true, 0));

        Assert.Equal(["Alpha"], SelectedNames(cut));
        Assert.Equal(1, FocusedRowIndex());
    }

    [Fact]
    public async Task ArrowKey_OutsideMultiSelectMode_MovesTheSelectionAndTheKeyboardToTheSameItem()
    {
        var cut = RenderPart([alpha, beta, gamma, delta]);
        cut.FindAll(RowSelector)[0].Click();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 0));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 1));
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 2));

        Assert.Equal(["Delta"], SelectedNames(cut));
        Assert.Equal(3, FocusedRowIndex());
    }

    [Fact]
    public void MenuEntriesThatKeepThePartOnScreen_ReturnTheFocusToIt()
    {
        var cut = RenderPart([alpha, beta, gamma], groups: [Group(Action("Run", CrudActionScope.Single))]);
        OpenMenu(cut, 1);
        var before = JSInterop.Invocations.Count(i => i.Identifier == "restoreFocus");

        MenuItem(cut, L("SelectMore")).Click();

        cut.WaitForAssertion(() => Assert.Equal(before + 1, JSInterop.Invocations.Count(i => i.Identifier == "restoreFocus")));
    }

    private int FocusedRowIndex()
        => (int)JSInterop.Invocations.Last(i => i.Identifier == "focusRow").Arguments[1]!;

    [Fact]
    public async Task ArrowDown_StopsAtTheLastItem()
    {
        var cut = RenderPart();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("ArrowDown", false, false, 2));

        Assert.Equal(["Gamma"], SelectedNames(cut));
    }

    [Fact]
    public async Task Space_SelectsTheFocusedItem_AndTogglesItWhileTheCirclesAreShown()
    {
        var cut = RenderPart();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, 1));
        Assert.Equal(["Beta"], SelectedNames(cut));

        cut.FindAll(RowSelector)[0].Click(new MouseEventArgs { CtrlKey = true });
        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, 2));
        Assert.Equal(["Alpha", "Beta", "Gamma"], SelectedNames(cut));

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync(" ", false, false, 1));
        Assert.Equal(["Alpha", "Gamma"], SelectedNames(cut));
    }

    [Fact]
    public async Task Enter_OpensTheFocusedItem()
    {
        var cut = RenderPart();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Enter", false, false, 1));

        Assert.Same(beta, Assert.Single(openedItems).Model);
    }

    [Fact]
    public async Task CtrlA_SelectsEveryItem_AndEscapeClearsTheSelectionAndLeavesMultiSelectMode()
    {
        var cut = RenderPart();

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("a", false, true, 0));
        Assert.Equal(["Alpha", "Beta", "Gamma"], SelectedNames(cut));
        Assert.Equal(3, cut.FindAll(".select-circle").Count);

        await cut.InvokeAsync(() => cut.Instance.OnKeyAsync("Escape", false, false, 0));
        Assert.Empty(SelectedNames(cut));
        Assert.Empty(cut.FindAll(".select-circle"));
    }

    #endregion

    #region Inline mode

    [Fact]
    public void Inline_ClickingAnInput_DoesNotChangeTheSelection()
    {
        var cut = RenderPart(mode: ListPartEditMode.Inline, card: InlineCard());

        Assert.Equal(3, cut.FindAll(".base-listpart-item-fields").Count);
        Assert.Empty(cut.FindAll(LabelSelector));

        Assert.Throws<MissingEventHandlerException>(() => cut.FindAll(".base-listpart-item-fields fluent-text-field")[0].Click());
        Assert.Throws<MissingEventHandlerException>(() => cut.FindAll(RowSelector)[1].Click());
        cut.FindAll(".base-listpart-item-fields fluent-text-field")[0].Change("Changed");

        Assert.Empty(SelectedNames(cut));
        Assert.Empty(cut.FindAll(".select-circle"));
    }

    [Fact]
    public void Inline_TheThreeDotButton_SelectsTheRowAndOffersDeleteAndTheSelectEntries_ButNoOpen()
    {
        var cut = RenderPart(mode: ListPartEditMode.Inline, card: InlineCard());

        OpenMenu(cut, 1);

        Assert.Equal([1], SelectedIndexes(cut));
        Assert.Equal([L("Delete"), Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")], MenuTokens(cut));
    }

    [Fact]
    public void Inline_SelectMore_ShowsTheGutterCircles_AndTheyToggleRows()
    {
        var cut = RenderPart(mode: ListPartEditMode.Inline, card: InlineCard());

        OpenMenu(cut, 0);
        MenuItem(cut, L("SelectMore")).Click();
        Assert.Equal(3, cut.FindAll(".select-circle").Count);

        cut.FindAll(".select-circle")[2].Click();

        Assert.Equal([0, 2], SelectedIndexes(cut));
    }

    [Fact]
    public void Inline_Delete_ThroughTheMenu_RemovesTheRowAndKeepsTheInputsEditable()
    {
        var items = new List<TestProduct> { alpha, beta, gamma };
        var cut = RenderPart(items, mode: ListPartEditMode.Inline, card: InlineCard());

        OpenMenu(cut, 2);
        MenuItem(cut, L("Delete")).Click();

        Assert.Equal(2, items.Count);
        Assert.Equal(2, cut.FindAll(".base-listpart-item-fields").Count);
    }

    [Fact]
    public void Inline_ReadOnlyPart_HasNoMenuEntriesAndNoEditableInputs()
    {
        var cut = RenderPart(mode: ListPartEditMode.Inline, card: InlineCard(), configure: parameters => parameters.Add(p => p.ReadOnly, true));

        Assert.Empty(cut.FindAll(MenuButtonSelector));
        Assert.Empty(cut.FindAll(".base-listpart-header fluent-button"));
    }

    #endregion

    #region Helpers

    private static BaseCardConfiguration<TestProduct> InlineCard() =>
        new BaseCardBuilder<TestProduct>().Field(product => product.Name).Build();

    private static List<string> SelectedNames(IRenderedComponent<BaseListPart<TestProduct>> cut)
        => [.. cut.FindAll(".base-listpart-item-selected").Select(row => row.QuerySelector(LabelSelector)!.TextContent.Trim())];

    private static List<int> SelectedIndexes(IRenderedComponent<BaseListPart<TestProduct>> cut)
        => [.. cut.FindAll(".base-listpart-item-selected").Select(row => int.Parse(row.GetAttribute("data-listpart-row")!))];
    private static List<string> MenuTokens<TModel>(IRenderedComponent<TModel> cut) where TModel : IComponent
        => [.. cut.Find(".context-menu").Children.Select(child => child.ClassList.Contains("context-menu-divider") ? Divider : child.TextContent.Trim())];

    private static AngleSharp.Dom.IElement MenuItem<TModel>(IRenderedComponent<TModel> cut, string text) where TModel : IComponent
        => cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

    private static void OpenMenu<TModel>(IRenderedComponent<TModel> cut, int row) where TModel : IComponent
    {
        cut.FindAll(MenuButtonSelector)[row].Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
    }

    private static bool IsToolbarDisabled(IRenderedComponent<BaseListPart<TestProduct>> cut) =>
        cut.Find(".base-action-toolbar fluent-button").HasAttribute("disabled");

    private static string? ToolbarReason(IRenderedComponent<BaseListPart<TestProduct>> cut) =>
        cut.FindAll(".base-action-toolbar-reason").FirstOrDefault()?.TextContent.Trim();

    private static CrudAction<TestProduct> Action(
        string caption,
        CrudActionScope scope,
        Func<TestProduct, bool>? enabled = null,
        Action<CrudActionEventArgs<TestProduct>>? run = null) => new()
        {
            Caption = caption,
            Scope = scope,
            Enabled = enabled,
            Action = args =>
            {
                run?.Invoke(args);
                return Task.CompletedTask;
            }
        };

    private static CrudActionGroup<TestProduct> Group(params CrudAction<TestProduct>[] actions) => new()
    {
        Name = "group",
        Contexts = CrudActionContext.ListPart,
        Actions = [.. actions]
    };

    private IRenderedComponent<BaseListPart<TestProduct>> RenderPart(
        List<TestProduct>? items = null,
        ListPartEditMode mode = ListPartEditMode.Dialog,
        string role = "Admin",
        List<CrudActionGroup<TestProduct>>? groups = null,
        BaseCardConfiguration<TestProduct>? card = null,
        Action<ComponentParameterCollectionBuilder<BaseListPart<TestProduct>>>? configure = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("user");
        authorization.SetRoles(role);

        var dialogService = Substitute.For<IDialogService>();
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult(DialogResult.Cancel()));
        dialogService.ShowDialogAsync<BaseListPartItemDialog<TestProduct>>(Arg.Any<object>(), Arg.Any<DialogParameters>())
            .Returns(call =>
            {
                openedItems.Add((BaseDialogData<TestProduct>)call[0]);
                return Task.FromResult(reference);
            });
        Services.AddSingleton(dialogService);

        return Render<BaseListPart<TestProduct>>(parameters =>
        {
            parameters
                .Add(p => p.Items, items ?? [alpha, beta, gamma])
                .Add(p => p.EditMode, mode)
                .Add(p => p.ActionGroups, groups)
                .Add(p => p.CardConfiguration, card);

            configure?.Invoke(parameters);
        });
    }

    #endregion
}
