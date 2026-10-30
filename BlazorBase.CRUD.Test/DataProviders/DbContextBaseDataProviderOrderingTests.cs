using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

/// <summary>
/// A page of a list is only well defined if the query is ordered: <c>Skip</c>/<c>Take</c> over an unordered
/// query may return the same rows in another order next time, so paging through a list skips some rows and
/// repeats others. The primary key is appended as the last tie-breaker.
/// </summary>
[Collection(DbContextBaseDataProviderCollection.Name)]
public class DbContextBaseDataProviderOrderingTests
{
    [Fact]
    public async Task NoSorts_OrdersByPrimaryKey_WhateverTheInsertionOrder()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, Product(3, "C"), Product(1, "A"), Product(2, "B"));

        var result = await Provider(db).GetListAsync(new BaseQuery());

        Assert.Equal(["A", "B", "C"], result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task Pages_AreDisjointAndCoverEveryRowOnce()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, Enumerable.Range(1, 5).Reverse().Select(i => Product(i, $"P{i}")).ToArray());

        var firstPage = await Provider(db).GetListAsync(new BaseQuery { Skip = 0, Take = 2 });
        var secondPage = await Provider(db).GetListAsync(new BaseQuery { Skip = 2, Take = 2 });
        var thirdPage = await Provider(db).GetListAsync(new BaseQuery { Skip = 4, Take = 2 });

        var names = firstPage.Items.Concat(secondPage.Items).Concat(thirdPage.Items).Select(p => p.Name);
        Assert.Equal(["P1", "P2", "P3", "P4", "P5"], names);
        Assert.Equal(5, thirdPage.TotalCount);
    }

    [Fact]
    public async Task SortByANonUniqueColumn_IsBrokenByThePrimaryKey()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, Product(4, "D", stock: 1), Product(2, "B", stock: 1), Product(3, "C", stock: 0), Product(1, "A", stock: 1));

        var query = new BaseQuery { Sorts = [new SortDescriptor { PropertyName = "Stock" }] };
        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["C", "A", "B", "D"], result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task ACallerSort_StillWinsOverThePrimaryKey()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, Product(1, "B"), Product(2, "A"), Product(3, "C"));

        var query = new BaseQuery { Sorts = [new SortDescriptor { PropertyName = "Name", Direction = SortDirection.Descending }] };
        var result = await Provider(db).GetListAsync(query);

        Assert.Equal(["C", "B", "A"], result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task AnOrderTheQueryCustomizerImposed_IsKeptWhenTheCallerAsksForNone()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, Product(1, "B"), Product(2, "A"), Product(3, "C"));
        var provider = new DbContextBaseDataProvider<TestProduct>(
            db.CreateContext(), [], queryCustomizer: query => query.OrderByDescending(p => p.Name));

        var result = await provider.GetListAsync(new BaseQuery());

        Assert.Equal(["C", "B", "A"], result.Items.Select(p => p.Name));
    }

    private static DbContextBaseDataProvider<TestProduct> Provider(SqliteInMemoryDatabase db)
        => new(db.CreateContext(), []);

    private static void Seed(SqliteInMemoryDatabase db, params TestProduct[] products)
    {
        using var context = db.CreateContext();
        context.Products.AddRange(products);
        context.SaveChanges();
    }

    private static TestProduct Product(int key, string name, int stock = 0) => new()
    {
        Id = new Guid(key, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
        Name = name,
        Stock = stock
    };
}
