using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[BenchmarkCategory("Projection")]
public class ProjectionBenchmarks : ServerBenchmarkBase
{
    private const int PageSize = 50;

    [Benchmark(Baseline = true, Description = "Raw EF: Select 2 fields")]
    public async Task<List<BenchProduct>> Raw_ListWithProjection()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new BenchProduct { Id = p.Id, Name = p.Name, Price = p.Price })
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query.Select 2 fields")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ListWithProjection()
        => await ProductProvider.Query()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => p.Id, p => p.Name, p => p.Price)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "Raw EF: Select 5 fields")]
    public async Task<List<BenchProduct>> Raw_ListWithWideProjection()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new BenchProduct
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Stock = p.Stock,
                CreatedAt = p.CreatedAt
            })
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "BB.CRUD: Query.Select 5 fields")]
    public async Task<BaseQueryResult<BenchProduct>> BlazorBase_ListWithWideProjection()
        => await ProductProvider.Query()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => p.Id, p => p.Name, p => p.Price, p => p.Stock, p => p.CreatedAt)
            .Take(PageSize)
            .ToListAsync();
}
