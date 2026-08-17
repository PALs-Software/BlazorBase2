using System.Text.Json;
using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Events;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Test.Infrastructure;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

[Collection(DbContextBaseDataProviderCollection.Name)]
public class DbContextBaseDataProviderTests
{
    #region GetListAsync

    [Fact]
    public async Task GetListAsync_NoFilter_ReturnsAllWithTotalCount()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(NewProduct("A"), NewProduct("B"), NewProduct("C")));

        var result = await ProductProvider(db).GetListAsync(new BaseQuery());

        Assert.Equal(3, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_Filter_ReturnsMatchesAndFilteredTotalCount()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(
            NewProduct("A", stock: 5),
            NewProduct("B", stock: 15),
            NewProduct("C", stock: 25)));

        var query = new BaseQuery
        {
            Filters = [new FilterDescriptor { PropertyName = "Stock", Operator = FilterOperator.GreaterThan, Value = 10 }]
        };

        var result = await ProductProvider(db).GetListAsync(query);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.True(p.Stock > 10));
    }

    [Fact]
    public async Task GetListAsync_Sort_OrdersAscending()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(NewProduct("C"), NewProduct("A"), NewProduct("B")));

        var query = new BaseQuery { Sorts = [new SortDescriptor { PropertyName = "Name" }] };

        var result = await ProductProvider(db).GetListAsync(query);

        Assert.Equal(["A", "B", "C"], result.Items.Select(p => p.Name));
    }

    [Fact]
    public async Task GetListAsync_Paging_SkipsAndTakes()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(Enumerable.Range(1, 5).Select(i => NewProduct($"P{i}", stock: i))));

        var query = new BaseQuery
        {
            Sorts = [new SortDescriptor { PropertyName = "Stock" }],
            Skip = 2,
            Take = 2
        };

        var result = await ProductProvider(db).GetListAsync(query);

        Assert.Equal([3, 4], result.Items.Select(p => p.Stock));
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task GetListAsync_Projection_LoadsOnlySelectedFields()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.Add(NewProduct("P", stock: 42, price: 9.99m)));

        var query = new BaseQuery { Select = ["Id", "Name"] };

        var result = await ProductProvider(db).GetListAsync(query);

        var item = Assert.Single(result.Items);
        Assert.Equal("P", item.Name);
        Assert.Equal(0, item.Stock);
        Assert.Equal(0m, item.Price);
    }

    [Fact]
    public async Task GetListAsync_Projection_ExcludesAlwaysExcludedProperty()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.Add(NewProduct("P", internalNotes: "secret")));

        var query = new BaseQuery { Select = ["Id", "Name", "InternalNotes"] };

        var result = await ProductProvider(db).GetListAsync(query);

        var item = Assert.Single(result.Items);
        Assert.Equal("P", item.Name);
        Assert.Equal(string.Empty, item.InternalNotes);
    }

    [Fact]
    public async Task GetListAsync_Projection_LoadsReferenceNavigation()
    {
        using var db = new SqliteInMemoryDatabase();
        var categoryId = Guid.NewGuid();
        Seed(db, ctx =>
        {
            ctx.Categories.Add(new TestCategory { Id = categoryId, Name = "Tools" });
            ctx.Products.Add(NewProduct("P", categoryId: categoryId));
        });

        var query = new BaseQuery { Select = ["Id", "Name", "Category"] };

        var result = await ProductProvider(db).GetListAsync(query);

        var item = Assert.Single(result.Items);
        Assert.NotNull(item.Category);
        Assert.Equal("Tools", item.Category!.Name);
    }

    [Fact]
    public async Task GetListAsync_Projection_MaterializesCollectionNavigation()
    {
        using var db = new SqliteInMemoryDatabase();
        var productId = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P");
            product.Id = productId;
            ctx.Products.Add(product);
            ctx.Reviews.AddRange(
                new TestReview { Id = Guid.NewGuid(), ProductId = productId, Author = "a", IsApproved = true, DisplayOrder = 0 },
                new TestReview { Id = Guid.NewGuid(), ProductId = productId, Author = "b", IsApproved = false, DisplayOrder = 1 });
        });

        var query = new BaseQuery { Select = ["Id", "Reviews"] };

        var result = await ProductProvider(db).GetListAsync(query);

        Assert.Equal(2, result.Items.Single().Reviews.Count);
    }

    [Fact]
    public async Task GetListAsync_FilteredInclude_FiltersAndSortsCollection()
    {
        using var db = new SqliteInMemoryDatabase();
        var productId = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P");
            product.Id = productId;
            ctx.Products.Add(product);
            ctx.Reviews.AddRange(
                new TestReview { Id = Guid.NewGuid(), ProductId = productId, Author = "x", Rating = 5, IsApproved = true, DisplayOrder = 2 },
                new TestReview { Id = Guid.NewGuid(), ProductId = productId, Author = "y", Rating = 1, IsApproved = false, DisplayOrder = 1 },
                new TestReview { Id = Guid.NewGuid(), ProductId = productId, Author = "z", Rating = 4, IsApproved = true, DisplayOrder = 0 });
        });

        var query = new BaseQuery
        {
            Select = ["Id", "Reviews"],
            NavigationFilters =
            [
                new NavigationFilter
                {
                    NavigationName = "Reviews",
                    Filters = [new FilterDescriptor { PropertyName = "IsApproved", Operator = FilterOperator.Equals, Value = true }],
                    Sorts = [new SortDescriptor { PropertyName = "DisplayOrder" }]
                }
            ]
        };

        var result = await ProductProvider(db).GetListAsync(query);

        var item = Assert.Single(result.Items);
        Assert.All(item.Reviews, r => Assert.True(r.IsApproved));
        Assert.Equal([0, 2], item.Reviews.Select(r => r.DisplayOrder));
    }

    [Fact]
    public async Task GetListAsync_RunsBeforeQueryInterceptor()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(
            NewProduct("A", active: true),
            NewProduct("B", active: true),
            NewProduct("C", active: false)));

        var provider = ProductProvider(db, new ActiveOnlyQueryInterceptor());

        var result = await provider.GetListAsync(new BaseQuery());

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.True(p.IsActive));
    }

    #endregion

    #region GetListAsync Page Size Clamp (CRUD-DA-05)

    [Fact]
    public async Task GetListAsync_TakeExceedsMaxPageSize_ClampsItemsButNotTotalCount()
    {
        var originalMaxPageSize = BaseQueryLimits.MaxPageSize;
        try
        {
            BaseQueryLimits.MaxPageSize = 1000;

            using var db = new SqliteInMemoryDatabase();
            Seed(db, ctx => ctx.Products.AddRange(
                Enumerable.Range(1, BaseQueryLimits.MaxPageSize + 50).Select(i => NewProduct($"P{i}", stock: i))));

            var result = await ProductProvider(db).GetListAsync(new BaseQuery { Take = int.MaxValue });

            Assert.Equal(BaseQueryLimits.MaxPageSize, result.Items.Count);
            Assert.Equal(BaseQueryLimits.MaxPageSize + 50, result.TotalCount);
        }
        finally
        {
            BaseQueryLimits.MaxPageSize = originalMaxPageSize;
        }
    }

    [Fact]
    public async Task GetListAsync_LoweredMaxPageSize_ClampsFurther()
    {
        var originalMaxPageSize = BaseQueryLimits.MaxPageSize;
        try
        {
            BaseQueryLimits.MaxPageSize = 3;

            using var db = new SqliteInMemoryDatabase();
            Seed(db, ctx => ctx.Products.AddRange(Enumerable.Range(1, 10).Select(i => NewProduct($"P{i}", stock: i))));

            var result = await ProductProvider(db).GetListAsync(new BaseQuery { Take = 100 });

            Assert.Equal(3, result.Items.Count);
            Assert.Equal(10, result.TotalCount);
        }
        finally
        {
            BaseQueryLimits.MaxPageSize = originalMaxPageSize;
        }
    }

    [Fact]
    public async Task GetListAsync_MaxPageSizeSetToZero_KeepsDefaultAndStillClamps()
    {
        var originalMaxPageSize = BaseQueryLimits.MaxPageSize;
        try
        {
            BaseQueryLimits.MaxPageSize = 0;

            Assert.Equal(1000, BaseQueryLimits.MaxPageSize);

            using var db = new SqliteInMemoryDatabase();
            Seed(db, ctx => ctx.Products.AddRange(Enumerable.Range(1, 10).Select(i => NewProduct($"P{i}", stock: i))));

            var result = await ProductProvider(db).GetListAsync(new BaseQuery { Take = 100 });

            Assert.Equal(10, result.Items.Count);
            Assert.Equal(10, result.TotalCount);
        }
        finally
        {
            BaseQueryLimits.MaxPageSize = originalMaxPageSize;
        }
    }

    [Fact]
    public async Task GetListAsync_MaxPageSizeSetToIntMaxValue_EffectivelyRemovesCap()
    {
        var originalMaxPageSize = BaseQueryLimits.MaxPageSize;
        try
        {
            BaseQueryLimits.MaxPageSize = int.MaxValue;

            using var db = new SqliteInMemoryDatabase();
            Seed(db, ctx => ctx.Products.AddRange(Enumerable.Range(1, 1500).Select(i => NewProduct($"P{i}", stock: i))));

            var result = await ProductProvider(db).GetListAsync(new BaseQuery { Take = int.MaxValue });

            Assert.Equal(1500, result.Items.Count);
            Assert.Equal(1500, result.TotalCount);
        }
        finally
        {
            BaseQueryLimits.MaxPageSize = originalMaxPageSize;
        }
    }

    [Fact]
    public async Task GetListAsync_TakeBelowMaxPageSize_UnaffectedByClamp()
    {
        var originalMaxPageSize = BaseQueryLimits.MaxPageSize;
        try
        {
            BaseQueryLimits.MaxPageSize = 1000;

            using var db = new SqliteInMemoryDatabase();
            Seed(db, ctx => ctx.Products.AddRange(Enumerable.Range(1, 5).Select(i => NewProduct($"P{i}", stock: i))));

            var result = await ProductProvider(db).GetListAsync(new BaseQuery { Take = 2 });

            Assert.Equal(2, result.Items.Count);
            Assert.Equal(5, result.TotalCount);
        }
        finally
        {
            BaseQueryLimits.MaxPageSize = originalMaxPageSize;
        }
    }

    #endregion

    #region GetByIdAsync / GetCountAsync

    [Fact]
    public async Task GetByIdAsync_WithoutSelect_ReturnsFullEntity()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 42);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(42, result!.Stock);
    }

    /// <summary>
    /// "No select" means the whole entity, and an entity is not whole without its children.
    /// </summary>
    /// <remarks>
    /// This is the path <c>BaseList</c> uses to open an edit dialog. Before the fix it resolved to
    /// <c>FindAsync</c>, which loads scalar columns only - so a <c>ListPartField</c> declared on the
    /// card rendered "No items." for a record that plainly had children, with no error anywhere.
    /// Lazy loading is deliberately off in BlazorBase, so nothing filled the gap later either.
    /// </remarks>
    [Fact]
    public async Task GetByIdAsync_WithoutSelect_LoadsCollectionNavigations()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();

        Seed(db, ctx =>
        {
            var product = NewProduct("P");
            product.Id = id;
            product.Reviews =
            [
                new TestReview { Id = Guid.NewGuid(), Author = "Ann", Rating = 5 },
                new TestReview { Id = Guid.NewGuid(), Author = "Bob", Rating = 3 }
            ];
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Reviews.Count);
        Assert.Contains(result.Reviews, review => review.Author == "Ann");
        Assert.Contains(result.Reviews, review => review.Author == "Bob");
    }

    [Fact]
    public async Task GetByIdAsync_WithoutSelect_ReturnsEmptyCollectionWhenThereAreNoChildren()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();

        Seed(db, ctx =>
        {
            var product = NewProduct("P");
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id);

        Assert.NotNull(result);
        Assert.Empty(result!.Reviews);
    }

    /// <summary>
    /// The scope-filtered overload takes a different branch, and an edit dialog opened under
    /// row-level security needs its children just as much.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_WithScopeFilterAndWithoutSelect_LoadsCollectionNavigations()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();

        Seed(db, ctx =>
        {
            var product = NewProduct("P", active: true);
            product.Id = id;
            product.Reviews = [new TestReview { Id = Guid.NewGuid(), Author = "Ann", Rating = 5 }];
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, select: null, scopeFilter: product => product.IsActive);

        Assert.NotNull(result);
        Assert.Single(result!.Reviews);
    }

    [Fact]
    public async Task GetByIdAsync_WithSelect_ReturnsProjectedEntity()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 42);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, select: ["Name"]);

        Assert.NotNull(result);
        Assert.Equal("P", result!.Name);
        Assert.Equal(0, result.Stock);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        using var db = new SqliteInMemoryDatabase();

        var result = await ProductProvider(db).GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountAsync_ReturnsTotalRowCount()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(NewProduct("A"), NewProduct("B"), NewProduct("C"), NewProduct("D")));

        var count = await ProductProvider(db).GetCountAsync();

        Assert.Equal(4, count);
    }

    #endregion

    #region Scope Filter (Row-Level Security)

    [Fact]
    public async Task GetListAsync_WithScopeFilter_ReturnsOnlyMatchingItemsAndScopedTotalCount()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(
            NewProduct("A", stock: 5),
            NewProduct("B", stock: 15),
            NewProduct("C", stock: 25)));

        var result = await ProductProvider(db).GetListAsync(new BaseQuery(), scopeFilter: p => p.Stock > 10);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, p => Assert.True(p.Stock > 10));
    }

    [Fact]
    public async Task GetCountAsync_WithScopeFilter_CountsOnlyScopedItems()
    {
        using var db = new SqliteInMemoryDatabase();
        Seed(db, ctx => ctx.Products.AddRange(
            NewProduct("A", stock: 5),
            NewProduct("B", stock: 15),
            NewProduct("C", stock: 25)));

        var count = await ProductProvider(db).GetCountAsync(scopeFilter: p => p.Stock > 10);

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task GetByIdAsync_WithScopeFilterExcludingId_ReturnsNull()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 5);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, scopeFilter: p => p.Stock > 10);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithScopeFilterIncludingId_ReturnsEntity()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 15);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, scopeFilter: p => p.Stock > 10);

        Assert.NotNull(result);
        Assert.Equal(15, result!.Stock);
    }

    [Fact]
    public async Task GetByIdAsync_WithSelectAndScopeFilterExcludingId_ReturnsNull()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 5);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, select: ["Name"], scopeFilter: p => p.Stock > 10);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_WithSelectAndScopeFilterIncludingId_ReturnsProjectedEntity()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 15);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var result = await ProductProvider(db).GetByIdAsync(id, select: ["Name"], scopeFilter: p => p.Stock > 10);

        Assert.NotNull(result);
        Assert.Equal("P", result!.Name);
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_PersistsEntity()
    {
        using var db = new SqliteInMemoryDatabase();

        var created = await ProductProvider(db).CreateAsync(NewProduct("New"));

        Assert.Equal("New", created.Name);
        await using var ctx = db.CreateContext();
        Assert.Equal(1, await ctx.Products.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_RunsBeforeCreateInterceptor()
    {
        using var db = new SqliteInMemoryDatabase();

        var provider = ProductProvider(db, new TrimNameInterceptor());
        var created = await provider.CreateAsync(NewProduct("   spaced   "));

        Assert.Equal("spaced", created.Name);
    }

    #endregion

    #region PatchAsync

    [Fact]
    public async Task PatchAsync_UpdatesOnlyChangedFields()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("old", stock: 5);
            product.Id = id;
            ctx.Products.Add(product);
        });

        await ProductProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["Name"] = "new" });

        await using var ctx = db.CreateContext();
        var updated = await ctx.Products.FindAsync(id);
        Assert.Equal("new", updated!.Name);
        Assert.Equal(5, updated.Stock);
    }

    [Fact]
    public async Task PatchAsync_IgnoresAlwaysExcludedProperty()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", internalNotes: "keep");
            product.Id = id;
            ctx.Products.Add(product);
        });

        await ProductProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["InternalNotes"] = "hacked" });

        await using var ctx = db.CreateContext();
        var updated = await ctx.Products.FindAsync(id);
        Assert.Equal("keep", updated!.InternalNotes);
    }

    [Fact]
    public async Task PatchAsync_ConvertsStringValueToTargetType()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 1);
            product.Id = id;
            ctx.Products.Add(product);
        });

        await ProductProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["Stock"] = "15" });

        await using var ctx = db.CreateContext();
        Assert.Equal(15, (await ctx.Products.FindAsync(id))!.Stock);
    }

    [Fact]
    public async Task PatchAsync_DeserializesJsonElementValue()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P", stock: 1);
            product.Id = id;
            ctx.Products.Add(product);
        });

        var jsonValue = JsonSerializer.SerializeToElement(7);
        await ProductProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["Stock"] = jsonValue });

        await using var ctx = db.CreateContext();
        Assert.Equal(7, (await ctx.Products.FindAsync(id))!.Stock);
    }

    [Fact]
    public async Task PatchAsync_UnknownId_Throws()
    {
        using var db = new SqliteInMemoryDatabase();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProductProvider(db).PatchAsync(Guid.NewGuid(), new Dictionary<string, object?> { ["Name"] = "x" }));
    }

    [Fact]
    public async Task PatchAsync_NullStamp_SkipsConcurrencyCheck()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1, CreatedOn = DateTime.UtcNow }));

        var result = await OrderProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["CustomerName"] = "B" });

        Assert.Equal("B", result.CustomerName);
    }

    [Fact]
    public async Task PatchAsync_AuditModel_CorrectStamp_Succeeds()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder
        {
            Id = id,
            CustomerName = "A",
            Total = 1,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = new DateTime(2026, 1, 1, 12, 0, 0)
        }));

        string stamp;
        await using (var ctx = db.CreateContext())
            stamp = (await ctx.Orders.FindAsync(id))!.ModifiedOn!.Value.ToString("O");

        var result = await OrderProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["CustomerName"] = "B" }, concurrencyStamp: stamp);

        Assert.Equal("B", result.CustomerName);
    }

    [Fact]
    public async Task PatchAsync_AuditModel_WrongStamp_ThrowsConcurrencyConflict()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder
        {
            Id = id,
            CustomerName = "A",
            Total = 1,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = new DateTime(2026, 1, 1, 12, 0, 0)
        }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => OrderProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["CustomerName"] = "B" },
                concurrencyStamp: new DateTime(2020, 1, 1).ToString("O")));
    }

    [Fact]
    public async Task PatchAsync_AuditModel_MissingModifiedOnWithStamp_ThrowsConcurrencyConflict()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder { Id = id, CustomerName = "A", Total = 1, CreatedOn = DateTime.UtcNow }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => OrderProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["CustomerName"] = "B" },
                concurrencyStamp: new DateTime(2026, 1, 1).ToString("O")));
    }

    [Fact]
    public async Task PatchAsync_InvalidStampFormat_ThrowsConcurrencyConflict()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder
        {
            Id = id,
            CustomerName = "A",
            Total = 1,
            CreatedOn = DateTime.UtcNow,
            ModifiedOn = new DateTime(2026, 1, 1, 12, 0, 0)
        }));

        await Assert.ThrowsAsync<ConcurrencyConflictException>(
            () => OrderProvider(db).PatchAsync(id, new Dictionary<string, object?> { ["CustomerName"] = "B" },
                concurrencyStamp: "not-a-date"));
    }

    [Fact]
    public async Task PatchAsync_DoesNotApplyClientSuppliedAuditFields()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx => ctx.Orders.Add(new TestOrder
        {
            Id = id,
            CustomerName = "A",
            Total = 1,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "original-author"
        }));

        await OrderProvider(db).PatchAsync(id, new Dictionary<string, object?>
        {
            ["CustomerName"] = "B",
            ["CreatedBy"] = "attacker"
        });

        await using var ctx = db.CreateContext();
        var updated = await ctx.Orders.FindAsync(id);
        Assert.Equal("B", updated!.CustomerName);
        Assert.Equal("original-author", updated.CreatedBy);
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_RemovesEntity()
    {
        using var db = new SqliteInMemoryDatabase();
        var id = Guid.NewGuid();
        Seed(db, ctx =>
        {
            var product = NewProduct("P");
            product.Id = id;
            ctx.Products.Add(product);
        });

        await ProductProvider(db).DeleteAsync(id);

        await using var ctx = db.CreateContext();
        Assert.Equal(0, await ctx.Products.CountAsync());
    }

    [Fact]
    public async Task DeleteAsync_UnknownId_Throws()
    {
        using var db = new SqliteInMemoryDatabase();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ProductProvider(db).DeleteAsync(Guid.NewGuid()));
    }

    #endregion

    #region Helpers

    private static DbContextBaseDataProvider<TestProduct> ProductProvider(
        SqliteInMemoryDatabase db,
        params IBaseDataInterceptor<TestProduct>[] interceptors)
    {
        return new DbContextBaseDataProvider<TestProduct>(db.CreateContext(), interceptors);
    }

    private static DbContextBaseDataProvider<TestOrder> OrderProvider(SqliteInMemoryDatabase db)
    {
        return new DbContextBaseDataProvider<TestOrder>(db.CreateContext(), []);
    }

    private static void Seed(SqliteInMemoryDatabase db, Action<TestDbContext> seed)
    {
        using var context = db.CreateContext();
        seed(context);
        context.SaveChanges();
    }

    private static TestProduct NewProduct(
        string name,
        int stock = 0,
        decimal price = 0m,
        bool active = true,
        ProductKind kind = ProductKind.Physical,
        Guid? categoryId = null,
        string internalNotes = "")
    {
        return new TestProduct
        {
            Id = Guid.NewGuid(),
            Name = name,
            Stock = stock,
            Price = price,
            IsActive = active,
            Kind = kind,
            CategoryId = categoryId,
            InternalNotes = internalNotes,
            ReleasedOn = new DateTime(2026, 1, 1)
        };
    }

    private sealed class TrimNameInterceptor : BaseDataInterceptor<TestProduct>
    {
        public override Task<TestProduct> OnBeforeCreateAsync(TestProduct model, CancellationToken cancellationToken = default)
        {
            model.Name = model.Name.Trim();
            return Task.FromResult(model);
        }
    }

    private sealed class ActiveOnlyQueryInterceptor : BaseDataInterceptor<TestProduct>
    {
        public override Task<BaseQuery> OnBeforeQueryAsync(BaseQuery query, CancellationToken cancellationToken = default)
        {
            query.Filters.Add(new FilterDescriptor { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = true });
            return Task.FromResult(query);
        }
    }

    #endregion
}
