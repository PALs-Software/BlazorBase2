using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A declared item-bound action shows up in the list toolbar (enabled by the selection) and in the row menu (enabled
/// for the clicked row), honours <c>Visible</c> in both places and marks the rows that block it.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListActionModelTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";

    private readonly TestProduct active = new() { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true };
    private readonly TestProduct inactive = new() { Id = Guid.NewGuid(), Name = "Beta", IsActive = false };

    public BaseListActionModelTests() => Services.AddLocalization();

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

    private static CrudAction<TestProduct> Action(string caption, CrudActionScope scope, Action<CrudActionEventArgs<TestProduct>>? onExecute = null) => new()
    {
        Caption = caption,
        Scope = scope,
        Action = args =>
        {
            onExecute?.Invoke(args);
            return Task.CompletedTask;
        }
    };

    private static CrudActionGroup<TestProduct> Group(params CrudAction<TestProduct>[] actions) => new()
    {
        Name = "group",
        Actions = [.. actions]
    };

    [Fact]
    public void RowMenu_OffersADeclaredSingleActionAndRunsItOnTheClickedRow()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var cut = RenderList(Group(Action("Run now", CrudActionScope.Single, args => received = args)));

        OpenRowMenu(cut, 1);
        cut.WaitForAssertion(() => Assert.Contains("Run now", cut.Find(".context-menu").TextContent));
        MenuItem(cut, "Run now").Click();

        Assert.NotNull(received);
        Assert.Equal(inactive.Id, received.Item!.Id);
        Assert.Equal(inactive.Id, Assert.Single(received.SelectedItems).Id);
        Assert.Equal(CrudActionContext.ContextMenu, received.Context);
    }

    [Fact]
    public void RowMenu_ShowsAnItemBoundActionButNotAnActionWithoutScopeByDefault()
    {
        var cut = RenderList(
            Group(Action("Bound", CrudActionScope.Single)),
            Group(Action("Free", CrudActionScope.None)));

        OpenRowMenu(cut, 0);

        cut.WaitForAssertion(() => Assert.Contains("Bound", cut.Find(".context-menu").TextContent));
        Assert.DoesNotContain("Free", cut.Find(".context-menu").TextContent);
        Assert.Contains("Free", cut.Find(".base-action-toolbar").TextContent);
        Assert.Contains("Bound", cut.Find(".base-action-toolbar").TextContent);
    }

    [Fact]
    public void RowMenu_AnExplicitContextOnAnActionWithoutScopeWins()
    {
        var free = Action("Free", CrudActionScope.None);
        free.Contexts = CrudActionContext.ContextMenu;
        var cut = RenderList(Group(free));

        OpenRowMenu(cut, 0);

        cut.WaitForAssertion(() => Assert.Contains("Free", cut.Find(".context-menu").TextContent));
    }

    [Fact]
    public void RowMenu_GreysOutAnEntryTheRowBlocksAndShowsTheReasonAsTooltip()
    {
        var invoked = false;
        var action = Action("Run now", CrudActionScope.Single, _ => invoked = true);
        action.Enabled = p => p.IsActive;
        action.DisabledReason = "Product is inactive";
        var cut = RenderList(Group(action));

        OpenRowMenu(cut, 1);

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu-item-disabled")));
        var entry = cut.Find(".context-menu-item-disabled");
        Assert.Equal("true", entry.GetAttribute("aria-disabled"));
        Assert.Equal(string.Format(Framework["ActionNotPossibleForRow"].Value) + ": Product is inactive", entry.GetAttribute("title"));

        Assert.Throws<Bunit.MissingEventHandlerException>(() => entry.Click());
        Assert.False(invoked);
    }

    [Fact]
    public void RowMenu_EnablesTheSameActionForARowThatPasses()
    {
        var action = Action("Run now", CrudActionScope.Single);
        action.Enabled = p => p.IsActive;
        var cut = RenderList(Group(action));

        OpenRowMenu(cut, 0);

        cut.WaitForAssertion(() => Assert.Contains("Run now", cut.Find(".context-menu").TextContent));
        Assert.Empty(cut.FindAll(".context-menu-item-disabled"));
    }

    [Fact]
    public void VisibleFalse_HidesTheActionInTheToolbarAndTheRowMenu()
    {
        var action = Action("Hidden", CrudActionScope.Single);
        action.Visible = _ => Task.FromResult(false);
        var cut = RenderList(Group(action));

        Assert.DoesNotContain("Hidden", cut.Find(".base-list-header").TextContent);

        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.DoesNotContain("Hidden", cut.Find(".context-menu").TextContent);
    }

    [Fact]
    public void VisibleForTheClickedRow_DecidesPerRowInTheRowMenu()
    {
        var action = Action("Only active", CrudActionScope.Single);
        action.Visible = args => Task.FromResult(args.Item is { IsActive: true });
        var cut = RenderList(Group(action));

        OpenRowMenu(cut, 1);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        Assert.DoesNotContain("Only active", cut.Find(".context-menu").TextContent);

        cut.Find(".context-menu-overlay").Click();
        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.Contains("Only active", cut.Find(".context-menu").TextContent));
    }

    [Fact]
    public void Toolbar_EnablesAMultipleActionOnceARowIsSelected()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.Multiple)));

        Assert.True(cut.Find(".base-action-toolbar .base-action-toolbar-button").HasAttribute("disabled"));

        SelectViaRowMenu(cut, 0);

        cut.WaitForAssertion(() => Assert.False(cut.Find(".base-action-toolbar .base-action-toolbar-button").HasAttribute("disabled")));
    }

    [Fact]
    public void Toolbar_MarksTheBlockingRowsWhileTheDisabledButtonIsHoveredOnly()
    {
        var action = Action("Run", CrudActionScope.Multiple);
        action.Enabled = p => p.IsActive;
        var cut = RenderList(Group(action));

        SelectAllViaRowMenu(cut);
        cut.WaitForAssertion(() => Assert.True(cut.Find(".base-action-toolbar .base-action-toolbar-button").HasAttribute("disabled")));
        Assert.Empty(cut.FindAll("tr.base-list-row-blocked"));

        cut.Find(".base-action-toolbar-item").MouseEnter();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tr.base-list-row-blocked")));
        Assert.Contains("Beta", cut.Find("tr.base-list-row-blocked").TextContent);

        cut.Find(".base-action-toolbar-item").MouseLeave();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("tr.base-list-row-blocked")));
    }

    [Fact]
    public void IsBulkAction_KeepsOfferingSelectionAndTheActionInTheRowMenu()
    {
        var cut = RenderList(Group(new CrudAction<TestProduct> { Caption = "Bulk", IsBulkAction = true, Action = _ => Task.CompletedTask }));

        OpenRowMenu(cut, 0);

        cut.WaitForAssertion(() => Assert.Contains("Bulk", cut.Find(".context-menu").TextContent));
    }

    [Fact]
    public void RowMenu_MultipleActionOnASelectedRowActsOnTheWholeSelection()
    {
        CrudActionEventArgs<TestProduct>? received = null;
        var cut = RenderList(Group(Action("Run", CrudActionScope.Multiple, args => received = args)));

        SelectAllViaRowMenu(cut);
        cut.WaitForAssertion(() => Assert.False(cut.Find(".base-action-toolbar .base-action-toolbar-button").HasAttribute("disabled")));
        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        MenuItem(cut, "Run").Click();

        Assert.NotNull(received);
        Assert.Equal(2, received.SelectedItems.Count);
        Assert.Equal(active.Id, received.Item!.Id);
    }

    private static AngleSharp.Dom.IElement MenuItem(IRenderedComponent<BaseList<TestProduct>> cut, string text) =>
        cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

    private void SelectViaRowMenu(IRenderedComponent<BaseList<TestProduct>> cut, int row)
    {
        OpenRowMenu(cut, row);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        MenuItem(cut, Framework["SelectMore"].Value).Click();
    }

    private void SelectAllViaRowMenu(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        MenuItem(cut, Framework["SelectAll"].Value).Click();
    }

    private static void OpenRowMenu(IRenderedComponent<BaseList<TestProduct>> cut, int row)
    {
        cut.WaitForAssertion(() => Assert.True(cut.FindAll(".base-list-row-menu").Count > row));
        cut.FindAll(".base-list-row-menu")[row].Click();
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList(params CrudActionGroup<TestProduct>[] groups)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<bool>("isPointOnDataRow", _ => true).SetResult(true);
        module.SetupVoid("rememberFocus", _ => true).SetVoidResult();
        module.SetupVoid("restoreFocus", _ => true).SetVoidResult();

        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct>
            {
                Items = [active, inactive],
                TotalCount = 2,
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ActionGroups, [.. groups])
            .Add(p => p.ChildContent, BuildColumn(p => p.Name)));

        cut.WaitForState(() => cut.FindAll(".base-list-row-menu").Count >= 2);

        return cut;
    }

    private static RenderFragment BuildColumn(Expression<Func<TestProduct, object?>> columnProperty)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), columnProperty);
            builder.AddComponentParameter(2, nameof(BaseColumn<TestProduct>.Sortable), false);
            builder.CloseComponent();
        };
    }
}
