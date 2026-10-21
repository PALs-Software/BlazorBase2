using System.Linq.Expressions;
using BlazorBase.CRUD.Components;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.DataProviders;
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
/// A column header sort used to re-order only the rows that were already loaded, so on a list with more than one
/// page the top rows were the first page's winners instead of the true ones. The sort now goes to the data provider
/// as the list's current sort, and paging starts over.
/// </summary>
[Collection(CustomPropertyResolutionCollection.Name)]
public class BaseListHeaderSortingTests : BunitTestContextBase
{
    private static readonly string[] Names = ["Echo", "Charlie", "Alpha", "Delta", "Bravo"];

    [Fact]
    public void HeaderSort_IsSentToTheProviderAndTheFirstPageIsLoadedAgainInThatOrder()
    {
        var provider = SortingProvider(Orders());
        var cut = RenderList(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.Equal(["Echo", "Charlie"], FirstColumn(cut)));

        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo"], FirstColumn(cut)));
        var sorted = provider.ReceivedQueries<TestOrder>().Last();
        var sort = Assert.Single(sorted.Sorts);
        Assert.Equal(nameof(TestOrder.CustomerName), sort.PropertyName);
        Assert.Equal(SortDirection.Ascending, sort.Direction);
        Assert.Equal(0, sorted.Skip);
    }

    [Fact]
    public void SecondClickOnTheHeader_SortsDescendingAcrossAllPages_NotJustTheLoadedRows()
    {
        var provider = SortingProvider(Orders());
        var cut = RenderList(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(FirstColumn(cut)));

        cut.Find("th .col-sort-button").Click();
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo"], FirstColumn(cut)));
        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Echo", "Delta"], FirstColumn(cut)));
        Assert.Equal(SortDirection.Descending, Assert.Single(provider.ReceivedQueries<TestOrder>().Last().Sorts).Direction);
    }

    [Fact]
    public void HeaderSort_StartsPagingOver_AndLoadMoreKeepsTheSort()
    {
        var provider = SortingProvider(Orders());
        var cut = RenderList(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".base-list-load-more")));
        cut.Find(".base-list-load-more").Click();
        cut.WaitForAssertion(() => Assert.Equal(4, FirstColumn(cut).Count));

        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo"], FirstColumn(cut)));
        var reloaded = provider.ReceivedQueries<TestOrder>().Last();
        Assert.Equal(0, reloaded.Skip);
        Assert.Equal(2, reloaded.Take);

        cut.Find(".base-list-load-more").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta"], FirstColumn(cut)));
        var nextPage = provider.ReceivedQueries<TestOrder>().Last();
        Assert.Equal(2, nextPage.Skip);
        Assert.Equal(nameof(TestOrder.CustomerName), Assert.Single(nextPage.Sorts).PropertyName);
    }

    [Fact]
    public void HeaderSort_OnANumericColumn_NamesTheProperty()
    {
        var provider = SortingProvider(Orders());
        var cut = RenderList(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(FirstColumn(cut)));

        cut.FindAll("th .col-sort-button")[1].Click();

        cut.WaitForAssertion(() => Assert.Equal(nameof(TestOrder.Total), Assert.Single(provider.ReceivedQueries<TestOrder>().Last().Sorts).PropertyName));
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo"], FirstColumn(cut)));
    }

    [Fact]
    public void InMemoryProvider_SortsEveryRow_NotOnlyTheFirstPage()
    {
        var orders = Orders();
        var provider = new InMemoryBaseDataProvider<TestOrder>(() => orders);
        var cut = RenderList(provider, pageSize: 2);
        cut.WaitForAssertion(() => Assert.Equal(5, FirstColumn(cut).Count));

        cut.Find("th .col-sort-button").Click();
        cut.WaitForAssertion(() => Assert.Equal(["Alpha", "Bravo", "Charlie", "Delta", "Echo"], FirstColumn(cut)));
        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(["Echo", "Delta", "Charlie", "Bravo", "Alpha"], FirstColumn(cut)));
    }

    [Fact]
    public void SortButton_IsNamedAfterTheColumn_NotAfterItsDescription()
    {
        var cut = RenderList(SortingProvider(Orders()), pageSize: 2, tooltip: "When this order was placed");
        cut.WaitForAssertion(() => Assert.NotEmpty(FirstColumn(cut)));

        var button = cut.Find("th .col-sort-button");

        Assert.Equal("CustomerName", button.GetAttribute("aria-label"));
        Assert.Equal("When this order was placed", cut.Find("th .base-list-header-title").GetAttribute("title"));
    }

    [Fact]
    public void SortedColumn_NamesItsDirectionInTheSortButton_AndOnlyThatColumn()
    {
        var cut = RenderList(SortingProvider(Orders()), pageSize: 2);
        cut.WaitForAssertion(() => Assert.NotEmpty(FirstColumn(cut)));

        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(SortedName("ColumnSortedAscending"), cut.Find("th .col-sort-button").GetAttribute("aria-label")));
        Assert.Equal("Total", cut.FindAll("th .col-sort-button")[1].GetAttribute("aria-label"));

        cut.Find("th .col-sort-button").Click();

        cut.WaitForAssertion(() => Assert.Equal(SortedName("ColumnSortedDescending"), cut.Find("th .col-sort-button").GetAttribute("aria-label")));
        Assert.Equal("CustomerName", cut.Find("th .base-list-header-title").TextContent.Trim());
    }

    private string SortedName(string key) =>
        string.Format(Services.GetRequiredService<IStringLocalizerFactory>().Create(typeof(BlazorBaseCrudResources))[key].Value, "CustomerName");

    private static List<TestOrder> Orders() =>
        [.. Names.Select((name, index) => new TestOrder { Id = Guid.NewGuid(), CustomerName = name, Total = 100 - index * 10 + (name == "Alpha" ? -50 : 0) })];

    private static List<string> FirstColumn(IRenderedComponent<BaseList<TestOrder>> cut) =>
        [.. cut.FindAll("tr.fluent-data-grid-row:not([row-type]) td:first-child").Select(cell => cell.TextContent.Trim())];

    private static IBaseDataProvider<TestOrder> SortingProvider(List<TestOrder> all)
    {
        var provider = Substitute.For<IBaseDataProvider<TestOrder>>();
        provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestOrder, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var query = call.Arg<BaseQuery>();
                IEnumerable<TestOrder> items = all;

                foreach (var sort in query.Sorts.AsEnumerable().Reverse())
                {
                    var property = typeof(TestOrder).GetProperty(sort.PropertyName)!;
                    items = sort.Direction == SortDirection.Descending
                        ? items.OrderByDescending(item => property.GetValue(item))
                        : items.OrderBy(item => property.GetValue(item));
                }

                return new BaseQueryResult<TestOrder> { Items = [.. items.Skip(query.Skip).Take(query.Take)], TotalCount = all.Count };
            });
        return provider;
    }

    private IRenderedComponent<BaseList<TestOrder>> RenderList(IBaseDataProvider<TestOrder> provider, int pageSize, string? tooltip = null)
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles("Admin");

        Services.AddLocalization();
        Services.AddSingleton(provider);

        return Render<BaseList<TestOrder>>(parameters => parameters
            .Add(p => p.DataProvider, provider)
            .Add(p => p.AllowAdd, false)
            .Add(p => p.PageSize, pageSize)
            .Add(p => p.ChildContent, BuildColumns(tooltip)));
    }

    private static RenderFragment BuildColumns(string? tooltip)
    {
        return builder =>
        {
            builder.OpenComponent<BaseColumn<TestOrder>>(0);
            builder.AddComponentParameter(1, nameof(BaseColumn<TestOrder>.Property), (Expression<Func<TestOrder, object?>>)(order => order.CustomerName));
            builder.AddComponentParameter(2, nameof(BaseColumn<TestOrder>.Tooltip), tooltip);
            builder.CloseComponent();
            builder.OpenComponent<BaseColumn<TestOrder>>(3);
            builder.AddComponentParameter(4, nameof(BaseColumn<TestOrder>.Property), (Expression<Func<TestOrder, object?>>)(order => order.Total));
            builder.CloseComponent();
        };
    }
}
