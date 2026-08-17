using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[BenchmarkCategory("List")]
public class ListQueryBenchmarks : ServerBenchmarkBase
{
    private const int PageSize = 50;

    [Benchmark(Baseline = true, Description = "Raw EF: Take 50")]
    public async Task<List<BenchProduct>> Raw_ListUnfiltered()
        => await CurrentContext.Products.AsNoTracking().Take(PageSize).ToListAsync();

    [Benchmark(Description = "BB.CRUD: GetListAsync (no filter)")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ListUnfiltered()
        => await ProductProvider.GetListAsync(new BaseQuery { Take = PageSize });

    [Benchmark(Description = "Raw EF: simple Where")]
    public async Task<List<BenchProduct>> Raw_ListSimpleWhere()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query simple Where")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ListSimpleWhere()
        => await ProductProvider.Query()
            .Where(p => p.IsActive)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "Raw EF: complex AND/OR Where")]
    public async Task<List<BenchProduct>> Raw_ListComplexWhere()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive && (p.Name.Contains("000") || p.Price < 100m))
            .OrderBy(p => p.Name)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query complex AND/OR Where")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ListComplexWhere()
        => await ProductProvider.Query()
            .Where(p => p.IsActive && (p.Name.Contains("000") || p.Price < 100m))
            .OrderBy(p => p.Name)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "Raw EF: GetCount")]
    public Task<int> Raw_GetCount()
        => CurrentContext.Products.CountAsync();

    [Benchmark(Description = "BB.CRUD: GetCountAsync")]
    public Task<int> BlazorBase_GetCount()
        => ProductProvider.GetCountAsync();
}
