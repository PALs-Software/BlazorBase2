using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[BenchmarkCategory("Read")]
public class ReadByIdBenchmarks : ServerBenchmarkBase
{
    private Guid TargetId { get; set; }

    private static readonly string[] SelectedFields = ["Name", "Price"];

    protected override void OnAfterGlobalSetup()
        => TargetId = DatabaseFixture.DeterministicGuid(2, RowCount / 2);

    [Benchmark(Baseline = true, Description = "Raw EF: FindAsync")]
    public async Task<BenchProduct?> Raw_FindAsync()
        => await CurrentContext.Products.FindAsync(TargetId);

    [Benchmark(Description = "BB.CRUD: GetByIdAsync")]
    public async Task<BenchProduct?> BlazorBase_GetByIdAsync()
        => await ProductProvider.GetByIdAsync(TargetId);

    [Benchmark(Description = "Raw EF: Where+Select projection")]
    public async Task<BenchProduct?> Raw_GetByIdWithProjection()
        => await CurrentContext.Products
            .AsNoTracking()
            .Where(p => p.Id == TargetId)
            .Select(p => new BenchProduct { Id = p.Id, Name = p.Name, Price = p.Price })
            .FirstOrDefaultAsync();

    [Benchmark(Description = "BB.CRUD: GetByIdAsync with select")]
    public async Task<BenchProduct?> BlazorBase_GetByIdAsyncWithSelect()
        => await ProductProvider.GetByIdAsync(TargetId, SelectedFields);
}
