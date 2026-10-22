using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Components;

/// <summary>
/// A list asks its data provider for its first rows exactly once when it opens, whichever layout it shows, and a page
/// that asks the list to refresh before that first load is done does not make it ask again.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListFirstLoadTests : BunitTestContextBase
{
    [Theory]
    [InlineData(BaseListMobileLayout.Never)]
    [InlineData(BaseListMobileLayout.Always)]
    public void OpeningTheList_SendsTheFirstQueryOnce(BaseListMobileLayout layout)
    {
        var provider = PagedProvider(total: 5);

        var cut = RenderList(provider, layout);

        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<TestOrder>()));
        Settle(provider);
        Assert.Single(provider.ReceivedQueries<TestOrder>());
        Assert.Equal(0, provider.ReceivedQueries<TestOrder>().Single().Skip);
    }

    [Theory]
    [InlineData(BaseListMobileLayout.Never)]
    [InlineData(BaseListMobileLayout.Always)]
    public async Task ARefreshRequestedBeforeTheFirstLoadIsDone_DoesNotSendAnotherQuery(BaseListMobileLayout layout)
    {
        var provider = PagedProvider(total: 5, delayMilliseconds: 250);

        var cut = RenderList(provider, layout);
        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());
        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<TestOrder>()));
        Settle(provider);
        Assert.Single(provider.ReceivedQueries<TestOrder>());
    }

    [Fact]
    public async Task ARefreshAfterTheFirstLoad_StillLoadsTheListAgain()
    {
        var provider = PagedProvider(total: 5);
        var cut = RenderList(provider, BaseListMobileLayout.Never);
        cut.WaitForAssertion(() => Assert.NotEmpty(provider.ReceivedQueries<TestOrder>()));
        Settle(provider);
        var before = provider.ReceivedQueries<TestOrder>().Count();

        await cut.InvokeAsync(() => cut.Instance.RefreshAsync());

        Assert.True(provider.ReceivedQueries<TestOrder>().Count() > before);
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(IBaseDataProvider<TestOrder> provider, BaseListMobileLayout layout)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");
        Services.AddLocalization();
        Services.AddSingleton(provider);

        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.PageSize, 50)
            .Add(p => p.MobileLayout, layout));
    }

    private static IBaseDataProvider<TestOrder> PagedProvider(int total, int delayMilliseconds = 0)
    {
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                if (delayMilliseconds > 0)
                    await Task.Delay(delayMilliseconds);

                var query = call.Arg<BaseQuery>();
                var count = Math.Max(0, Math.Min(query.Take, total - query.Skip));
                return new BaseQueryResult<TestOrder> { Items = [.. Enumerable.Range(0, count).Select(_ => new TestOrder())], TotalCount = total };
            });
        return provider;
    }

    private static void Settle(IBaseDataProvider<TestOrder> provider)
    {
        var last = -1;
        var stable = 0;

        for (var attempt = 0; attempt < 100 && stable < 8; attempt++)
        {
            Thread.Sleep(50);
            var count = provider.ReceivedQueries<TestOrder>().Count();
            stable = count == last && count > 0 ? stable + 1 : 0;
            last = count;
        }
    }
}
