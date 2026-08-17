using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[BenchmarkCategory("Navigation")]
public class NavigationIncludeBenchmarks : ServerBenchmarkBase
{
    private const int PageSize = 25;

    [Benchmark(Baseline = true, Description = "Raw EF: Include filtered child collection")]
    public async Task<List<BenchOrder>> Raw_OrdersWithFilteredItems()
        => await CurrentContext.Orders
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.OrderDate)
            .Include(o => o.Items.Where(i => i.IsActive).OrderBy(i => i.DisplayOrder))
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query.Include with filter")]
    public async Task<BaseQueryResult<BenchOrder>> BlazorBase_OrdersWithFilteredItems()
        => await OrderProvider.Query()
            .Where(o => o.IsActive)
            .OrderBy(o => o.OrderDate)
            .Include<BenchOrderItem>(o => o.Items, nav => nav
                .Where(i => i.IsActive)
                .OrderBy(i => i.DisplayOrder))
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "Raw EF: Include simple reference")]
    public async Task<List<BenchProduct>> Raw_ProductsWithCategory()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Include(p => p.Category)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query.Include simple reference")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ProductsWithCategory()
        => await ProductProvider.Query()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Include(p => p.Category)
            .Take(PageSize)
            .ToListAsync();
}
