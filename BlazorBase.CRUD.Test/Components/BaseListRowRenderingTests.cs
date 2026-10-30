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
/// Rows are plain markup that grows with every loaded page, so rebuilding all of them for something they do not show
/// froze a list of a few hundred rows for seconds whenever a menu or the filter panel opened.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListRowRenderingTests : BunitTestContextBase
{
    private const string InteropModulePath = "./_content/BlazorBase.CRUD/js/baseListInterop.js";

    private int CellRenders;

    [Fact]
    public void OpeningTheContextMenu_DoesNotRenderTheRowsAgain()
    {
        var cut = RenderList();
        var before = WaitUntilRowsAreStable();

        cut.Find(".base-list-grid").ContextMenu(new MouseEventArgs { ClientX = 40, ClientY = 40 });
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".context-menu")));

        Assert.Equal(before, CellRenders);
    }

    [Fact]
    public void OpeningTheFilterPanel_DoesNotRenderTheRowsAgain()
    {
        var cut = RenderList();
        var before = WaitUntilRowsAreStable();

        cut.Find(".base-list-filter-toggle").Click();
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".filter-panel")));

        Assert.Equal(before, CellRenders);
    }

    [Fact]
    public void ReloadingTheData_RendersTheRowsAgain()
    {
        var cut = RenderList();
        var before = WaitUntilRowsAreStable();

        cut.InvokeAsync(() => cut.Instance.RefreshAsync()).GetAwaiter().GetResult();

        cut.WaitForAssertion(() => Assert.True(CellRenders > before));
    }

    [Fact]
    public void RenderingTheListAgainFromItsParent_RendersTheRowsAgain()
    {
        var cut = RenderList();
        var before = WaitUntilRowsAreStable();

        cut.Render();

        cut.WaitForAssertion(() => Assert.True(CellRenders > before));
    }

    /// <summary>
    /// A virtualized grid renders its rows in several steps (the rows, then the data that arrives for them), so the
    /// count of renders is only a baseline once it has stopped moving.
    /// </summary>
    private int WaitUntilRowsAreStable()
    {
        var last = -1;
        var stable = 0;

        for (var attempt = 0; attempt < 100 && stable < 6; attempt++)
        {
            Thread.Sleep(50);
            stable = CellRenders == last && CellRenders > 0 ? stable + 1 : 0;
            last = CellRenders;
        }

        Assert.True(CellRenders > 0);

        return CellRenders;
    }

    private IRenderedComponent<BaseList<TestProduct>> RenderList()
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
                Items = [new TestProduct { Id = Guid.NewGuid(), Name = "Alpha", IsActive = true }],
                TotalCount = 1,
            });
        Services.AddSingleton(provider);

        return Render<BaseList<TestProduct>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.ChildContent, BuildCountingColumn()));
    }

    private RenderFragment BuildCountingColumn()
    {
        Expression<Func<TestProduct, object?>> property = product => product.Name;
        RenderFragment<TestProduct> template = product => builder =>
        {
            CellRenders++;
            builder.AddContent(0, product.Name);
        };

        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestProduct>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestProduct>.Property), property);
            builder.AddComponentParameter(2, nameof(BaseColumn<TestProduct>.Sortable), false);
            builder.AddComponentParameter(3, nameof(BaseColumn<TestProduct>.Template), template);
            builder.CloseComponent();
        };
    }
}
