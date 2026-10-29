using System.Linq.Expressions;
using AngleSharp.Dom;
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
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The list header holds Add and the declared toolbar actions on the left, the count, and the filter on the far right (no Edit, View or Delete button:
/// those live in the row menu), shows the selection info only in multi-select mode, and lets the declared actions
/// collapse into the "more actions" menu on narrow widths.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListToolbarLayoutTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";

    private readonly TestProduct active = new() { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true };
    private readonly TestProduct inactive = new() { Id = Guid.NewGuid(), Name = "Beta", IsActive = false };

    public BaseListToolbarLayoutTests() => Services.AddLocalization();

    private IStringLocalizer Framework => Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));

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

    [Fact]
    public void Header_HoldsNoEditViewOrDeleteButton()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.Single)));

        var captions = cut.FindAll(".base-list-header .base-list-header-button, .base-list-header .base-action-toolbar-button").Select(button => button.TextContent.Trim()).ToList();

        Assert.DoesNotContain(Framework["Edit"].Value, captions);
        Assert.DoesNotContain(Framework["View"].Value, captions);
        Assert.DoesNotContain(Framework["Delete"].Value, captions);
        Assert.Contains(Framework["Add"].Value, captions);
        Assert.Contains("Run", captions);
    }

    [Fact]
    public void Header_ShowsAddBeforeTheDeclaredActions()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.None)));

        var buttons = cut.FindAll(".base-list-header .base-list-header-button, .base-list-header .base-action-toolbar-button").Select(button => button.TextContent.Trim()).ToList();

        Assert.True(buttons.IndexOf(Framework["Add"].Value) < buttons.IndexOf("Run"));
        Assert.Equal("true", cut.Find(".base-list-header .base-action-toolbar").GetAttribute("data-overflow"));
    }

    [Fact]
    public void Header_OrdersAddThenToolbarThenTheFilterGroupLast()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.None)));

        var children = cut.Find(".base-list-header").Children.ToList();
        var add = children.FindIndex(element => element.ClassList.Contains("base-list-add"));
        var toolbar = children.FindIndex(element => element.ClassList.Contains("base-action-toolbar"));
        var filterGroup = children.FindIndex(element => element.ClassList.Contains("base-list-filter-group"));

        Assert.True(add >= 0);
        Assert.True(add < toolbar);
        Assert.True(toolbar < filterGroup);
        Assert.Equal(children.Count - 1, filterGroup);
    }

    [Fact]
    public void Header_AddIsTheAccentButton()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.None)));

        var add = cut.FindAll(".base-list-header .base-list-header-button, .base-list-header .base-action-toolbar-button").First(button => button.TextContent.Trim() == Framework["Add"].Value);

        Assert.Equal("primary", add.GetAttribute("appearance"));
    }

    [Fact]
    public void SelectionInfo_IsHiddenForASingleSelectionAndShownInMultiSelectMode()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.Multiple)));

        cut.FindAll("tr.base-list-row:not([row-type]) td[role=gridcell]:not(.base-list-row-menu-column)")[1].Click();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tr.base-list-row-selected")));

        Assert.Empty(cut.FindAll(".selection-count"));
        Assert.DoesNotContain(Framework["DeselectAll"].Value, cut.Find(".base-list-header").TextContent);

        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        MenuItem(cut, Framework["SelectAll"].Value).Click();

        cut.WaitForAssertion(() => Assert.Equal(string.Format(Framework["SelectedCount"].Value, 2), cut.Find(".selection-count").TextContent.Trim()));
        Assert.Contains(Framework["DeselectAll"].Value, cut.Find(".base-list-header").TextContent);

        cut.FindAll(".base-list-header .base-list-header-button, .base-list-header .base-action-toolbar-button").First(button => button.TextContent.Trim() == Framework["DeselectAll"].Value).Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".selection-count")));
    }

    [Fact]
    public void Overflow_OffersTheMoreActionsButtonNextToTheInlineActions()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.None)));

        var more = cut.Find(".base-action-toolbar-more .base-action-toolbar-button");

        Assert.Equal(Framework["MoreActions"].Value, more.GetAttribute("aria-label"));
        Assert.Equal("menu", more.GetAttribute("aria-haspopup"));
        Assert.Empty(cut.FindAll(".base-action-toolbar-menu"));
    }

    [Fact]
    public void Overflow_KeepsTheEnabledStateAndReasonOfItemBoundActions()
    {
        var cut = RenderList(Group(Action("Run", CrudActionScope.Single), Action("Import", CrudActionScope.None)));

        OpenMore(cut);

        var run = OverflowItem(cut, "Run");
        var import = OverflowItem(cut, "Import");

        Assert.Equal("true", run.GetAttribute("aria-disabled"));
        Assert.Equal(Framework["ActionSelectRowFirst"].Value, run.GetAttribute("title"));
        Assert.Null(import.GetAttribute("aria-disabled"));

        cut.FindAll("tr.base-list-row:not([row-type]) td[role=gridcell]:not(.base-list-row-menu-column)")[0].Click();

        cut.WaitForAssertion(() => Assert.Null(OverflowItem(cut, "Run").GetAttribute("aria-disabled")));
    }

    [Fact]
    public void Overflow_MarksTheBlockingRowsWhileTheDisabledEntryIsHovered()
    {
        var action = Action("Run", CrudActionScope.Multiple);
        action.Enabled = product => product.IsActive;
        var cut = RenderList(Group(action));

        OpenRowMenu(cut, 0);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        MenuItem(cut, Framework["SelectAll"].Value).Click();
        cut.WaitForAssertion(() => Assert.True(cut.Find(".base-action-toolbar .base-action-toolbar-button").HasAttribute("disabled")));

        OpenMore(cut);
        OverflowItem(cut, "Run").MouseEnter();

        cut.WaitForAssertion(() => Assert.Contains("Beta", Assert.Single(cut.FindAll("tr.base-list-row-blocked")).TextContent));

        OverflowItem(cut, "Run").MouseLeave();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("tr.base-list-row-blocked")));
    }

    private static void OpenMore(IRenderedComponent<BaseList<TestProduct>> cut)
    {
        cut.Find(".base-action-toolbar-more .base-action-toolbar-button").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-action-toolbar-menu")));
    }

    private static IElement OverflowItem(IRenderedComponent<BaseList<TestProduct>> cut, string text) =>
        cut.FindAll(".base-action-toolbar-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

    private static IElement MenuItem(IRenderedComponent<BaseList<TestProduct>> cut, string text) =>
        cut.FindAll(".context-menu [role=\"menuitem\"]").First(item => item.TextContent.Trim() == text);

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
