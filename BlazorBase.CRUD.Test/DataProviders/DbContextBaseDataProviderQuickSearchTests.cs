using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

/// <summary>
/// The quick search filter against a real EF Core provider: an OR group of "contains" conditions, ANDed with another filter,
/// translated to SQL. SQLite matches case-sensitively, so the texts below use the case of the data.
/// </summary>
[Collection(DbContextBaseDataProviderCollection.Name)]
public class DbContextBaseDataProviderQuickSearchTests
{
    [Fact]
    public async Task TheOrGroup_MatchesARowWhenAnyPropertyContainsTheText()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, NewProduct("Alpha widget", "plain"), NewProduct("Beta", "a widget too"), NewProduct("Gamma", "plain"));
        var query = new BaseQuery { Filters = [QuickSearchFilterBuilder.Build(["Name", "Description"], "widget")!] };

        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["Alpha widget", "Beta"], result.Items.Select(product => product.Name).Order());
        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task TheSearch_IsCombinedByAnd_WithAnotherFilter()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, NewProduct("Alpha widget", "plain", stock: 1), NewProduct("Beta", "a widget too", stock: 50));
        var query = new BaseQuery
        {
            Filters =
            [
                new FilterDescriptor { PropertyName = "Stock", Operator = FilterOperator.GreaterThan, Value = 10 },
                QuickSearchFilterBuilder.Build(["Name", "Description"], "widget")!
            ]
        };

        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["Beta"], result.Items.Select(product => product.Name));
    }

    [Fact]
    public async Task WildcardCharactersInTheText_AreMatchedLiterally()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, NewProduct("100% pure", "x"), NewProduct("Plain", "y"));
        var query = new BaseQuery { Filters = [QuickSearchFilterBuilder.Build(["Name", "Description"], "%")!] };

        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["100% pure"], result.Items.Select(product => product.Name));
    }

    [Fact]
    public async Task ASearchThroughANavigationPath_FindsTheRelatedRow()
    {
        using var db = new SqliteInMemoryDatabase();
        var category = new TestCategory { Id = Guid.NewGuid(), Name = "Tools" };
        using (var context = db.CreateContext())
        {
            context.Add(category);
            context.SaveChanges();
        }

        var withCategory = NewProduct("Hammer", "x");
        withCategory.CategoryId = category.Id;
        Seed(db, withCategory, NewProduct("Pillow", "y"));

        var query = new BaseQuery { Filters = [QuickSearchFilterBuilder.Build(["Category.Name"], "Tool")!] };

        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["Hammer"], result.Items.Select(product => product.Name));
    }

    private static DbContextBaseDataProvider<TestProduct> Provider(SqliteInMemoryDatabase db) => new(db.CreateContext(), []);

    private static void Seed(SqliteInMemoryDatabase db, params TestProduct[] products)
    {
        using var context = db.CreateContext();
        context.Products.AddRange(products);
        context.SaveChanges();
    }

    private static TestProduct NewProduct(string name, string description, int stock = 0) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        Stock = stock,
        ReleasedOn = new DateTime(2026, 1, 1)
    };
}
