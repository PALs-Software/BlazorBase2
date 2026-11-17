using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.Filtering;

public class QuickSearchFilterBuilderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankText_BuildsNoFilter(string? searchText)
    {
        Assert.Null(QuickSearchFilterBuilder.Build(["Name"], searchText));
    }

    [Fact]
    public void NoProperties_BuildsNoFilter()
    {
        Assert.Null(QuickSearchFilterBuilder.Build([], "widget"));
    }

    [Fact]
    public void OneProperty_BuildsASingleContainsCondition()
    {
        var filter = QuickSearchFilterBuilder.Build(["Name"], "widget");

        Assert.NotNull(filter);
        Assert.False(filter.IsGroup);
        Assert.Equal("Name", filter.PropertyName);
        Assert.Equal(FilterOperator.Contains, filter.Operator);
        Assert.Equal("widget", filter.Value);
    }

    [Fact]
    public void SeveralProperties_BuildAnOrGroupOfContainsConditions()
    {
        var filter = QuickSearchFilterBuilder.Build(["Name", "Description"], "widget");

        Assert.NotNull(filter);
        Assert.True(filter.IsGroup);
        Assert.Equal(FilterLogic.Or, filter.Logic);
        Assert.Equal(["Name", "Description"], filter.Filters!.Select(condition => condition.PropertyName));
        Assert.All(filter.Filters!, condition =>
        {
            Assert.Equal(FilterOperator.Contains, condition.Operator);
            Assert.Equal("widget", condition.Value);
        });
    }

    [Fact]
    public void TheTextIsTrimmed()
    {
        var filter = QuickSearchFilterBuilder.Build(["Name"], "  widget \t");

        Assert.Equal("widget", filter!.Value);
    }

    [Fact]
    public async Task OnTheInMemoryProvider_ARowMatchesWhenAnyPropertyContainsTheText_IgnoringCase()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => Catalog());
        var query = new BaseQuery { Filters = [QuickSearchFilterBuilder.Build(["Name", "Description"], "GREEN")!] };

        var result = await provider.GetListAsync(query);

        Assert.Equal(["Alpha", "Beta", "Green Gamma"], result.Items.Select(product => product.Name).Order());
    }

    [Fact]
    public async Task OnTheInMemoryProvider_TheSearchIsCombinedWithOtherFiltersByAnd()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => Catalog());
        var query = new BaseQuery
        {
            Filters =
            [
                new FilterDescriptor { PropertyName = nameof(TestProduct.IsActive), Operator = FilterOperator.Equals, Value = true },
                QuickSearchFilterBuilder.Build(["Name", "Description"], "green")!
            ]
        };

        var result = await provider.GetListAsync(query);

        Assert.Equal(["Alpha"], result.Items.Select(product => product.Name));
    }

    [Fact]
    public async Task OnTheInMemoryProvider_ARowWithoutAnyMatchIsLeftOut()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => Catalog());
        var query = new BaseQuery { Filters = [QuickSearchFilterBuilder.Build(["Name", "Description"], "nothing like this")!] };

        var result = await provider.GetListAsync(query);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    private static List<TestProduct> Catalog() =>
    [
        new() { Id = Guid.NewGuid(), Name = "Alpha", Description = "Green and shiny", IsActive = true },
        new() { Id = Guid.NewGuid(), Name = "Beta", Description = "Dull green", IsActive = false },
        new() { Id = Guid.NewGuid(), Name = "Green Gamma", Description = null, IsActive = false },
        new() { Id = Guid.NewGuid(), Name = "Delta", Description = "Blue", IsActive = true }
    ];
}
