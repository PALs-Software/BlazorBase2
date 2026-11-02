using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

public class InMemoryBaseDataProviderTests
{
    private class KeylessItem
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task GetListAsync_ReturnsCurrentAccessorSnapshot()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid(), Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetListAsync(new BlazorBase.CRUD.Models.BaseQuery());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("A", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task GetByIdAsync_FindsARowByItsKeyWhenTheKeyIsNotNamedId()
    {
        var id = Guid.NewGuid();
        var items = new List<TestTypeNameKeyed> { new() { TestTypeNameKeyedId = id, Name = "A" }, new() { TestTypeNameKeyedId = Guid.NewGuid(), Name = "B" } };
        var provider = new InMemoryBaseDataProvider<TestTypeNameKeyed>(() => items);

        var match = await provider.GetByIdAsync(id);

        Assert.Equal("A", match?.Name);
    }

    private static List<TestProduct> CreateCatalog() =>
    [
        new() { Id = Guid.NewGuid(), Name = "Alpha Widget", Price = 10m, Stock = 5, IsActive = true, ReleasedOn = new DateTime(2026, 1, 10, 14, 30, 0), Kind = ProductKind.Physical, Description = "first" },
        new() { Id = Guid.NewGuid(), Name = "beta gadget", Price = 25.5m, Stock = 0, IsActive = false, ReleasedOn = new DateTime(2026, 2, 1), Kind = ProductKind.Digital, PreferredKind = ProductKind.Service },
        new() { Id = Guid.NewGuid(), Name = "Gamma Widget", Price = 40m, Stock = 12, IsActive = true, ReleasedOn = new DateTime(2026, 3, 15), Kind = ProductKind.Service, Description = "third", Category = new TestCategory { Name = "Tools" } }
    ];

    private static async Task<List<string>> QueryNamesAsync(List<TestProduct> items, BaseQuery query)
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);
        var result = await provider.GetListAsync(query);

        Assert.Equal(result.Items.Count, result.TotalCount);
        return result.Items.Select(item => item.Name).ToList();
    }

    private static BaseQuery FilterBy(string property, FilterOperator filterOperator, object? value = null) =>
        new() { Filters = [new FilterDescriptor { PropertyName = property, Operator = filterOperator, Value = value }] };

    [Fact]
    public async Task GetListAsync_ContainsFilter_MatchesIgnoringCase()
    {
        var names = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Name), FilterOperator.Contains, "WIDGET"));

        Assert.Equal(["Alpha Widget", "Gamma Widget"], names);
    }

    [Fact]
    public async Task GetListAsync_StartsWithAndEndsWithFilters_Match()
    {
        var starts = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Name), FilterOperator.StartsWith, "beta"));
        var ends = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Name), FilterOperator.EndsWith, "gadget"));

        Assert.Equal(["beta gadget"], starts);
        Assert.Equal(["beta gadget"], ends);
    }

    [Fact]
    public async Task GetListAsync_EqualsAndNotEqualsFilters_OnTextNumberBoolAndEnum()
    {
        var byName = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Name), FilterOperator.Equals, "ALPHA WIDGET"));
        var byStock = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Stock), FilterOperator.Equals, 12));
        var byFlag = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.IsActive), FilterOperator.NotEquals, "true"));
        var byKind = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Kind), FilterOperator.Equals, "Digital"));

        Assert.Equal(["Alpha Widget"], byName);
        Assert.Equal(["Gamma Widget"], byStock);
        Assert.Equal(["beta gadget"], byFlag);
        Assert.Equal(["beta gadget"], byKind);
    }

    [Fact]
    public async Task GetListAsync_RangeFilters_OnNumbers()
    {
        var query = new BaseQuery
        {
            Filters =
            [
                new FilterDescriptor { PropertyName = nameof(TestProduct.Price), Operator = FilterOperator.GreaterThanOrEqual, Value = 10 },
                new FilterDescriptor { PropertyName = nameof(TestProduct.Price), Operator = FilterOperator.LessThan, Value = "40" }
            ]
        };

        var names = await QueryNamesAsync(CreateCatalog(), query);

        Assert.Equal(["Alpha Widget", "beta gadget"], names);
    }

    [Fact]
    public async Task GetListAsync_DateFilterWithoutTime_MatchesWholeDay()
    {
        var equalsDay = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.ReleasedOn), FilterOperator.Equals, new DateTime(2026, 1, 10)));
        var upToDay = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.ReleasedOn), FilterOperator.LessThanOrEqual, new DateTime(2026, 1, 10)));
        var afterDay = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.ReleasedOn), FilterOperator.GreaterThan, "2026-02-01"));

        Assert.Equal(["Alpha Widget"], equalsDay);
        Assert.Equal(["Alpha Widget"], upToDay);
        Assert.Equal(["Gamma Widget"], afterDay);
    }

    [Fact]
    public async Task GetListAsync_NullFilters_OnNullableProperties()
    {
        var withoutDescription = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Description), FilterOperator.IsNull));
        var withPreferredKind = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.PreferredKind), FilterOperator.IsNotNull));

        Assert.Equal(["beta gadget"], withoutDescription);
        Assert.Equal(["beta gadget"], withPreferredKind);
    }

    [Fact]
    public async Task GetListAsync_InFilter_MatchesAnyValue()
    {
        var names = await QueryNamesAsync(CreateCatalog(), FilterBy(nameof(TestProduct.Stock), FilterOperator.In, new[] { 0, 5 }));

        Assert.Equal(["Alpha Widget", "beta gadget"], names);
    }

    [Fact]
    public async Task GetListAsync_NestedPropertyPath_IsResolved()
    {
        var names = await QueryNamesAsync(CreateCatalog(), FilterBy("Category.Name", FilterOperator.Equals, "tools"));

        Assert.Equal(["Gamma Widget"], names);
    }

    [Fact]
    public async Task GetListAsync_FilterGroup_CombinesWithOrAndAnd()
    {
        var orGroup = new FilterDescriptor
        {
            Logic = FilterLogic.Or,
            Filters =
            [
                new FilterDescriptor { PropertyName = nameof(TestProduct.Name), Operator = FilterOperator.StartsWith, Value = "alpha" },
                new FilterDescriptor { PropertyName = nameof(TestProduct.Stock), Operator = FilterOperator.GreaterThan, Value = 10 }
            ]
        };
        var activeOnly = new FilterDescriptor { PropertyName = nameof(TestProduct.IsActive), Operator = FilterOperator.Equals, Value = true };

        var names = await QueryNamesAsync(CreateCatalog(), new BaseQuery { Filters = [orGroup, activeOnly] });

        Assert.Equal(["Alpha Widget", "Gamma Widget"], names);
    }

    [Fact]
    public async Task GetListAsync_UnsupportedConditions_AreIgnored()
    {
        var query = new BaseQuery
        {
            Filters =
            [
                new FilterDescriptor { PropertyName = "DoesNotExist", Operator = FilterOperator.Equals, Value = "x" },
                new FilterDescriptor { PropertyName = nameof(TestProduct.Stock), Operator = FilterOperator.Equals, Value = "not a number" },
                new FilterDescriptor { PropertyName = nameof(TestProduct.Name), Operator = FilterOperator.Contains, Value = null }
            ]
        };

        var names = await QueryNamesAsync(CreateCatalog(), query);

        Assert.Equal(3, names.Count);
    }

    [Fact]
    public async Task GetListAsync_NoMatch_ReturnsEmptyResultWithZeroTotal()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(CreateCatalog);

        var result = await provider.GetListAsync(FilterBy(nameof(TestProduct.Name), FilterOperator.Contains, "zzz"));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_Sorts_AscendingDescendingAndThenBy()
    {
        var ascending = await QueryNamesAsync(CreateCatalog(), new BaseQuery { Sorts = [new SortDescriptor { PropertyName = nameof(TestProduct.Name) }] });
        var descending = await QueryNamesAsync(CreateCatalog(), new BaseQuery { Sorts = [new SortDescriptor { PropertyName = nameof(TestProduct.Stock), Direction = SortDirection.Descending }] });
        var thenBy = await QueryNamesAsync(CreateCatalog(), new BaseQuery
        {
            Sorts =
            [
                new SortDescriptor { PropertyName = nameof(TestProduct.IsActive), Direction = SortDirection.Descending },
                new SortDescriptor { PropertyName = nameof(TestProduct.Price), Direction = SortDirection.Descending }
            ]
        });

        Assert.Equal(["Alpha Widget", "beta gadget", "Gamma Widget"], ascending);
        Assert.Equal(["Gamma Widget", "Alpha Widget", "beta gadget"], descending);
        Assert.Equal(["Gamma Widget", "Alpha Widget", "beta gadget"], thenBy);
    }

    [Fact]
    public async Task GetListAsync_SortsNullsFirst()
    {
        var names = await QueryNamesAsync(CreateCatalog(), new BaseQuery { Sorts = [new SortDescriptor { PropertyName = nameof(TestProduct.Description) }] });

        Assert.Equal("beta gadget", names[0]);
    }

    [Fact]
    public async Task GetListAsync_IgnoresSkipAndTake_ReturnsWholeMatchingList()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(CreateCatalog);

        var firstPage = await provider.GetListAsync(new BaseQuery { Skip = 0, Take = 2 });
        var nextPage = await provider.GetListAsync(new BaseQuery { Skip = firstPage.Items.Count, Take = 2 });

        Assert.Equal(3, firstPage.Items.Count);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(firstPage.Items.Count, firstPage.TotalCount);
        Assert.Equal(3, nextPage.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_AppliesScopeFilter()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(CreateCatalog);

        var result = await provider.GetListAsync(new BaseQuery(), item => item.IsActive);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, item => Assert.True(item.IsActive));
    }

    [Fact]
    public async Task GetListAsync_ReflectsLiveListReplacement()
    {
        var items = CreateCatalog();
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        items = [new TestProduct { Id = Guid.NewGuid(), Name = "Only" }];
        var result = await provider.GetListAsync(new BaseQuery());

        Assert.Equal(["Only"], result.Items.Select(item => item.Name));
    }

    [Fact]
    public async Task GetByIdAsync_FindsEntityByReflectedIdProperty()
    {
        var id = Guid.NewGuid();
        var items = new List<TestProduct> { new() { Id = id, Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetByIdAsync(id);

        Assert.Equal("A", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid(), Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ModelWithoutIdProperty_ReturnsNull()
    {
        var items = new List<KeylessItem> { new() { Name = "A" } };
        var provider = new InMemoryBaseDataProvider<KeylessItem>(() => items);

        var result = await provider.GetByIdAsync("anything");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountAsync_MatchesListLength()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var count = await provider.GetCountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.CreateAsync(new TestProduct()));
    }

    [Fact]
    public async Task PatchAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.PatchAsync(Guid.NewGuid(), new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.DeleteAsync(Guid.NewGuid()));
    }
}
