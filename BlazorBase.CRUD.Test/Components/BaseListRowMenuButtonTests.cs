using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The row menu button sits in a narrow column right of the open column, is shown by the stylesheet on hover, selection,
/// focus and an open menu, and opens the menu in the Business Central order: View or Edit, Delete, the declared actions,
/// then "Select more", "Select all" and "Deselect all".
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListRowMenuButtonTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";
    private const string RowSelector = "tr.fluent-data-grid-row:not([row-type])";
    private const string Divider = "-";

    private readonly List<BaseDialogData<TestProduct>> openedCards = [];
    private readonly TestProduct first = new() { Id = Guid.NewGuid(), Name = "Alpha", Stock = 1, IsActive = true };
    private readonly TestProduct second = new() { Id = Guid.NewGuid(), Name = "Beta", Stock = 2, IsActive = true };

    public BaseListRowMenuButtonTests() => Services.AddLocalization();

    private string L(string key) => Framework[key].Value;

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    [Fact]
    public void Button_IsTheCellDirectlyAfterTheFirstColumn_AndNothingStaysAtTheEnd()
    {
        var cut = RenderList();

        var cells = DataCells(cut, 0);

        Assert.Equal(3, cells.Count);
        Assert.Contains("base-list-open-cell", cells[0].ClassName);
        Assert.Contains("base-list-row-menu-column", cells[1].ClassName);
        Assert.NotNull(cells[1].QuerySelector(".base-list-row-menu"));
        Assert.DoesNotContain("base-list-row-menu-column", cells[2].ClassName);
        Assert.Empty(cut.FindAll(".base-list-actions-column"));
        Assert.Equal(2, cut.FindAll(".base-list-row-menu").Count);
    }

    [Fact]
    public void Button_FollowsTheColumnThatOpensTheRow()
    {
        var cut = RenderList(opensItemColumn: 1);

        var cells = DataCells(cut, 0);

        Assert.Contains("base-list-open-cell", cells[1].ClassName);
        Assert.Contains("base-list-row-menu-column", cells[2].ClassName);
    }

    [Fact]
    public void Button_StaysAfterTheOpenColumnWhileTheSelectCirclesAreShown()
    {
        var cut = RenderList();
        OpenMenu(cut, 0);
        MenuItem(cut, L("SelectMore")).Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".select-circle")));
        var cells = cut.FindAll(RowSelector)[0].QuerySelectorAll("td[role=gridcell]");

        Assert.Contains("base-list-select-column", cells[0].ClassName);
        Assert.Contains("base-list-open-cell", cells[1].ClassName);
        Assert.Contains("base-list-row-menu-column", cells[2].ClassName);
    }

    [Fact]
    public void Button_KeepsItsLabelAndMenuSemantics()
    {
        var cut = RenderList();

        var button = cut.FindAll(".base-list-row-menu")[0];

        Assert.Equal(Framework["MoreActions"].Value, button.GetAttribute("aria-label"));
        Assert.Equal("menu", button.GetAttribute("aria-haspopup"));
    }

    [Fact]
    public void ListWithoutMenuEntries_HasNoButtonColumn()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false, withCard: false);

        Assert.Empty(cut.FindAll(".base-list-row-menu-column"));
        Assert.Equal(2, DataCells(cut, 0).Count);
    }

    [Fact]
    public void ClickingTheButton_SelectsItsRowWhenItIsNotSelected()
    {
        var cut = RenderList();

        OpenMenu(cut, 1);

        Assert.Contains("base-list-row-selected", cut.FindAll(RowSelector)[1].ClassName);
        Assert.DoesNotContain("base-list-row-selected", cut.FindAll(RowSelector)[0].ClassName);
    }

    [Fact]
    public void ClickingTheButton_KeepsTheMultiSelectionWhenItsRowIsPartOfIt()
    {
        var cut = RenderList();
        OpenMenu(cut, 0);
        MenuItem(cut, L("SelectAll")).Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));

        OpenMenu(cut, 1);

        Assert.Equal(2, cut.FindAll("tr.base-list-row-selected").Count);
        Assert.Equal(string.Format(Framework["DeleteCount"].Value, 2), MenuTokens(cut)[1]);
    }

    [Fact]
    public void TheRowOfTheOpenMenu_CarriesTheMenuOpenClass_UntilTheMenuCloses()
    {
        var cut = RenderList();

        OpenMenu(cut, 1);

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("base-list-row-menu-open", cut.FindAll(RowSelector)[1].ClassName);
            Assert.DoesNotContain("base-list-row-menu-open", cut.FindAll(RowSelector)[0].ClassName);
        });

        cut.Find(".context-menu-overlay").Click();

        cut.WaitForAssertion(() => Assert.DoesNotContain("base-list-row-menu-open", cut.FindAll(RowSelector)[1].ClassName));
    }

    [Fact]
    public void EditableList_MenuOrderIsEditDeleteDeclaredActionsThenTheSelectionEntries()
    {
        var cut = RenderList(groups: [Group(Action("Run now", CrudActionScope.Single))]);

        OpenMenu(cut, 0);

        Assert.Equal(
            [L("Edit"), L("Delete"), "Run now", Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            MenuTokens(cut));
    }

    [Fact]
    public void EditableList_PutsTheLegacyContextMenuActionsAfterTheDeclaredOnes_WithADivider()
    {
        var cut = RenderList(
            groups: [Group(Action("Run now", CrudActionScope.Single))],
            contextMenuActions: _ => builder => builder.AddContent(0, "legacy"));

        OpenMenu(cut, 0);

        Assert.Equal(
            [L("Edit"), L("Delete"), "Run now", Divider, "legacy", Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            MenuTokens(cut));
    }

    [Fact]
    public void ReadOnlyList_OffersViewInsteadOfEdit_ThenTheDeclaredActionsAndSelection()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false, groups: [Group(Action("Run now", CrudActionScope.Multiple))]);

        OpenMenu(cut, 0);

        Assert.Equal(
            [L("View"), "Run now", Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")],
            MenuTokens(cut));
    }

    [Fact]
    public void ReadOnlyListWithoutActions_OffersOnlyView()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false);

        OpenMenu(cut, 0);

        Assert.Equal([L("View")], MenuTokens(cut));
    }

    [Fact]
    public void ViewEntry_OpensTheReadOnlyCard()
    {
        var cut = RenderList(allowEdit: false, allowDelete: false);
        OpenMenu(cut, 1);

        MenuItem(cut, L("View")).Click();

        cut.WaitForAssertion(() => Assert.Single(openedCards));
        Assert.True(openedCards[0].ReadOnly);
        Assert.Equal(second.Id, openedCards[0].Model.Id);
    }

    [Fact]
    public void EditEntry_OpensTheEditableCard()
    {
        var cut = RenderList();
        OpenMenu(cut, 0);

        MenuItem(cut, L("Edit")).Click();

        cut.WaitForAssertion(() => Assert.Single(openedCards));
        Assert.False(openedCards[0].ReadOnly);
    }

    [Fact]
    public void SelectMore_EntersMultiSelectModeAndKeepsTheClickedRowSelected()
    {
        var cut = RenderList();
        OpenMenu(cut, 1);
        Assert.Empty(cut.FindAll(".select-circle"));

        MenuItem(cut, L("SelectMore")).Click();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".select-circle").Count));
        Assert.Single(cut.FindAll("tr.base-list-row-selected"));
        Assert.Contains("base-list-row-selected", cut.FindAll(RowSelector)[1].ClassName);
        Assert.Empty(cut.FindAll(".context-menu"));
    }

    [Fact]
    public void WhileTheSelectCirclesAreShown_TheMenuOffersNoSelectMoreButStillDeselectAll()
    {
        var cut = RenderList();
        OpenMenu(cut, 0);
        MenuItem(cut, L("SelectMore")).Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));

        OpenMenu(cut, 0);

        Assert.DoesNotContain(L("SelectMore"), MenuTokens(cut));
        Assert.Contains(L("SelectAll"), MenuTokens(cut));

        MenuItem(cut, L("DeselectAll")).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".select-circle")));
        Assert.Empty(cut.FindAll("tr.base-list-row-selected"));
    }

    [Fact]
    public async Task ShiftF10OnAFocusedCell_OpensTheSameMenuForThatRow()
    {
        var cut = RenderList();
        DataCells(cut, 1)[0].Focus();

        await cut.InvokeAsync(() => cut.Instance.OnGridContextMenuKeyAsync(10, 20, 42, 52));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.Equal([L("Edit"), L("Delete"), Divider, L("SelectMore"), L("SelectAll"), L("DeselectAll")], MenuTokens(cut));
        Assert.Contains("base-list-row-selected", cut.FindAll(RowSelector)[1].ClassName);
    }

    [Fact]
    public async Task ShiftF10WithoutAFocusedCell_OpensNothing()
    {
        var cut = RenderList();

        await cut.InvokeAsync(() => cut.Instance.OnGridContextMenuKeyAsync(10, 20, 42, 52));

        Assert.Empty(cut.FindAll(".context-menu"));
    }

    private static List<string> MenuTokens(IRenderedComponent<BaseList<TestProduct>> cut)
        => [.. cut.Find(".context-menu").Children.Select(child => child.ClassList.Contains("context-menu-divider") ? Divider : child.TextContent.Trim())];

    private static AngleSharp.Dom.IElement MenuItem(IRenderedComponent<BaseList<TestProduct>> cut, string text)
        => cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

    private static void OpenMenu(IRenderedComponent<BaseList<TestProduct>> cut, int row)
    {
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(".base-list-row-menu").Count > row));
        cut.FindAll(".base-list-row-menu")[row].Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
    }

    private static List<AngleSharp.Dom.IElement> DataCells(IRenderedComponent<BaseList<TestProduct>> cut, int row)
    {
        cut.WaitForState(() => cut.FindAll(RowSelector).Count > row);
        return [.. cut.FindAll(RowSelector)[row].QuerySelectorAll("td[role=gridcell]")];
    }

    private static CrudAction<TestProduct> Action(string caption, CrudActionScope scope) => new()
    {
        Caption = caption,
        Scope = scope,
        Action = _ => Task.CompletedTask
    };

    private static CrudActionGroup<TestProduct> Group(params CrudAction<TestProduct>[] actions) => new()
    {
        Name = "group",
        Actions = [.. actions]
    };

    private IRenderedComponent<BaseList<TestProduct>> RenderList(
        bool allowEdit = true,
        bool allowDelete = true,
        bool withCard = true,
        int? opensItemColumn = null,
        List<CrudActionGroup<TestProduct>>? groups = null,
        RenderFragment<TestProduct>? contextMenuActions = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<bool>("isPointOnDataRow", _ => true).SetResult(true);
        module.SetupVoid("rememberFocus", _ => true).SetVoidResult();
        module.SetupVoid("restoreFocus", _ => true).SetVoidResult();

        var dialogService = Substitute.For<IDialogService>();
        var reference = Substitute.For<IDialogReference>();
        reference.Result.Returns(Task.FromResult(DialogResult.Cancel()));
        dialogService.ShowDialogAsync<BaseDialog<TestProduct>>(Arg.Any<object>(), Arg.Any<DialogParameters>())
            .Returns(call =>
            {
                openedCards.Add((BaseDialogData<TestProduct>)call[0]);
                return Task.FromResult(reference);
            });
        Services.AddSingleton(dialogService);

        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct>
            {
                Items = [first, second],
                TotalCount = 2,
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.AllowEdit, allowEdit)
            .Add(p => p.AllowDelete, allowDelete)
            .Add(p => p.EnableDeepLinking, false)
            .Add(p => p.ActionGroups, groups)
            .Add(p => p.ContextMenuActions, contextMenuActions)
            .Add(p => p.CardConfiguration, withCard ? new BlazorBase.CRUD.Configuration.BaseCardConfiguration<TestProduct>() : null)
            .Add(p => p.ChildContent, BuildColumns(opensItemColumn)));

        cut.WaitForState(() => cut.FindAll(RowSelector).Count >= 2);

        return cut;
    }

    private static RenderFragment BuildColumns(int? opensItemColumn)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.Name));
            builder.AddComponentParameter(2, nameof(BaseColumn<TestProduct>.Sortable), false);
            builder.AddComponentParameter(3, nameof(BaseColumn<TestProduct>.OpensItem), opensItemColumn == 0);
            builder.CloseComponent();

            builder.OpenComponent<BaseColumn<TestProduct>>(4);
            builder.AddComponentParameter(5, nameof(BaseColumn<TestProduct>.Property), (Expression<Func<TestProduct, object?>>)(p => p.Stock));
            builder.AddComponentParameter(6, nameof(BaseColumn<TestProduct>.Sortable), false);
            builder.AddComponentParameter(7, nameof(BaseColumn<TestProduct>.OpensItem), opensItemColumn == 1);
            builder.CloseComponent();
        };
    }
}
