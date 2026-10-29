using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// The row context menu opens over a full-viewport overlay that swallows every click outside it, so
/// whatever cannot be reached with the keyboard cannot be left with the keyboard either.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListContextMenuTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";

    [Fact]
    public void EscapeClosesTheContextMenu()
    {
        var cut = RenderListWithOpenContextMenu();

        cut.Find(".context-menu").KeyDown(Key.Escape);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));
    }

    [Fact]
    public void ContextMenu_IsAMenuTheKeyboardCanReach()
    {
        var cut = RenderListWithOpenContextMenu();
        var menu = cut.Find(".context-menu");

        Assert.Equal("menu", menu.GetAttribute("role"));
        Assert.Equal("-1", menu.GetAttribute("tabindex"));
    }

    /// <summary>
    /// Dismissing puts the keyboard back where it came from; acting on an entry that opens a dialog or a confirmation does
    /// not, because that manages its own focus.
    /// </summary>
    [Fact]
    public void DismissingTheMenuRestoresFocus_OpeningADialogFromItDoesNot()
    {
        var cut = RenderListWithOpenContextMenu();

        cut.Find(".context-menu").KeyDown(Key.Escape);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));
        var afterDismiss = MenuFocusRestores();
        Assert.Equal(1, afterDismiss);

        cut.Find(".base-list-grid").ContextMenu(new MouseEventArgs { ClientX = 40, ClientY = 40 });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));
        cut.FindAll(".context-menu [role=\"menuitem\"]").First().Click();
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));

        Assert.Equal(afterDismiss, MenuFocusRestores());
    }

    /// <summary>
    /// A menu entry that keeps the list on screen (select more, select all, deselect all) hands the keyboard back to the
    /// list like closing the menu does; otherwise the keyboard would be dropped on the page after the click.
    /// </summary>
    [Theory]
    [InlineData("SelectMore")]
    [InlineData("SelectAll")]
    [InlineData("DeselectAll")]
    public void SelectionEntriesOfTheMenu_ReturnTheFocusToTheList(string entry)
    {
        Services.AddLocalization();
        var cut = RenderListWithOpenContextMenu(rows: 3);
        var framework = Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources));
        var before = MenuFocusRestores();
        var item = cut.FindAll(".context-menu [role=\"menuitem\"]").FirstOrDefault(menuItem => menuItem.TextContent.Trim() == framework[entry].Value);

        Assert.NotNull(item);
        item.Click();

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".context-menu")));
        cut.WaitForAssertion(() => Assert.Equal(before + 1, MenuFocusRestores()));
    }

    private int MenuFocusRestores()
        => JSInterop.Invocations.Count(i => i.Identifier == "restoreFocus" && i.Arguments.FirstOrDefault() is string key && key.StartsWith("context-menu-", StringComparison.Ordinal));

    private IRenderedComponent<BaseList<TestProduct>> RenderListWithOpenContextMenu(int rows = 1)
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
                Items = [.. Enumerable.Range(0, rows).Select(index => new TestProduct { Id = Guid.NewGuid(), Name = index == 0 ? "Alpha" : $"Row {index}", IsActive = true })],
                TotalCount = rows,
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(p => p.Name)));

        cut.WaitForState(() => cut.FindAll(".base-list-grid").Count > 0);
        cut.WaitForState(() => cut.FindAll("tr.base-list-row:not([row-type]) td").Count > 0);
        cut.Find("tr.base-list-row:not([row-type]) td").Focus();
        cut.Find(".base-list-grid").ContextMenu(new MouseEventArgs { ClientX = 40, ClientY = 40 });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));

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
