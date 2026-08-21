using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
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

    private IRenderedComponent<BaseList<TestProduct>> RenderListWithOpenContextMenu()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        var module = JSInterop.SetupModule(InteropModulePath);
        module.Setup<bool>("isPointOnDataRow", _ => true).SetResult(true);

        var provider = Substitute.For<IBaseDataProvider<TestProduct>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct>
            {
                Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true }],
                TotalCount = 1,
            });
        Services.AddSingleton(provider);

        var cut = Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildColumn(p => p.Name)));

        cut.WaitForState(() => cut.FindAll(".base-list-grid").Count > 0);
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
