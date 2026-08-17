using System.Linq.Expressions;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using NSubstitute;
using Xunit;

namespace BlazorBase.CRUD.Test.Querying;

public class BaseQueryBuilderTests
{
    private readonly IBaseDataProvider<TestProduct> Provider = Substitute.For<IBaseDataProvider<TestProduct>>();

    [Fact]
    public void Where_AddsDecomposedFilter()
    {
        var query = Provider.Query().Where(p => p.IsActive).Build();

        var filter = Assert.Single(query.Filters);
        Assert.Equal("IsActive", filter.PropertyName);
        Assert.Equal(FilterOperator.Equals, filter.Operator);
    }

    [Fact]
    public void MultipleWhere_AddsMultipleFilters()
    {
        var query = Provider.Query()
            .Where(p => p.IsActive)
            .Where(p => p.Stock > 10)
            .Build();

        Assert.Equal(2, query.Filters.Count);
    }

    [Fact]
    public void OrderBy_AddsAscendingSort()
    {
        var query = Provider.Query().OrderBy(p => p.Name).Build();

        var sort = Assert.Single(query.Sorts);
        Assert.Equal("Name", sort.PropertyName);
        Assert.Equal(SortDirection.Ascending, sort.Direction);
    }

    [Fact]
    public void OrderByDescending_AddsDescendingSort()
    {
        var query = Provider.Query().OrderByDescending(p => p.Stock).Build();

        var sort = Assert.Single(query.Sorts);
        Assert.Equal("Stock", sort.PropertyName);
        Assert.Equal(SortDirection.Descending, sort.Direction);
    }

    [Fact]
    public void Select_AddsProjectionFields()
    {
        var query = Provider.Query().Select(p => p.Id, p => p.Name).Build();

        Assert.Equal(["Id", "Name"], query.Select);
    }

    [Fact]
    public void Include_AddsNavigationToSelect()
    {
        var query = Provider.Query().Select(p => p.Id).Include(p => p.Category).Build();

        Assert.Contains("Category", query.Select!);
    }

    [Fact]
    public void FilteredInclude_AddsNavigationFilter()
    {
        var query = Provider.Query()
            .Include<TestReview>(p => p.Reviews, nav => nav
                .Where(r => r.IsApproved)
                .OrderBy(r => r.DisplayOrder))
            .Build();

        Assert.Contains("Reviews", query.Select!);

        var navFilter = Assert.Single(query.NavigationFilters!);
        Assert.Equal("Reviews", navFilter.NavigationName);
        Assert.Single(navFilter.Filters);
        var sort = Assert.Single(navFilter.Sorts);
        Assert.Equal("DisplayOrder", sort.PropertyName);
    }

    [Fact]
    public void SkipAndTake_ArePropagated()
    {
        var query = Provider.Query().Skip(5).Take(15).Build();

        Assert.Equal(5, query.Skip);
        Assert.Equal(15, query.Take);
    }

    [Fact]
    public void Build_WithoutPaging_KeepsBaseQueryDefaults()
    {
        var query = Provider.Query().Build();

        Assert.Equal(0, query.Skip);
        Assert.Equal(50, query.Take);
        Assert.Null(query.Select);
    }

    [Fact]
    public async Task ToListAsync_DelegatesToProvider()
    {
        Provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new BaseQueryResult<TestProduct> { Items = [], TotalCount = 0 });

        await Provider.Query().Where(p => p.IsActive).ToListAsync();

        await Provider.Received(1).GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FirstOrDefaultAsync_LimitsToOneAndReturnsFirstItem()
    {
        var first = new TestProduct { Name = "first" };
        var second = new TestProduct { Name = "second" };
        BaseQuery? captured = null;

        Provider.GetListAsync(Arg.Any<BaseQuery>(), Arg.Any<Expression<Func<TestProduct, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<BaseQuery>();
                return new BaseQueryResult<TestProduct> { Items = [first, second] };
            });

        var result = await Provider.Query().FirstOrDefaultAsync();

        Assert.Same(first, result);
        Assert.Equal(1, captured!.Take);
    }
}
