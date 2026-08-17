using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[BenchmarkCategory("Write")]
public class WriteBenchmarks : ServerBenchmarkBase
{
    private Guid CategoryIdForCreate { get; set; }

    private Guid TargetUpdateId { get; set; }

    private static readonly Dictionary<string, object?> SingleFieldPatch = new()
    {
        ["Price"] = 9.99m
    };

    private static readonly Dictionary<string, object?> WidePatch = new()
    {
        ["Price"] = 9.99m,
        ["Stock"] = 50,
        ["Description"] = "Updated",
        ["IsActive"] = false,
        ["Name"] = "Updated Product"
    };

    protected override void OnAfterGlobalSetup()
    {
        CategoryIdForCreate = DatabaseFixture.DeterministicGuid(1, 0);
        TargetUpdateId = DatabaseFixture.DeterministicGuid(2, RowCount / 2);
    }

    [Benchmark(Baseline = true, Description = "Raw EF: Add + SaveChanges")]
    public async Task<BenchProduct> Raw_Create()
    {
        var product = NewProduct();
        CurrentContext.Products.Add(product);
        await CurrentContext.SaveChangesAsync();
        return product;
    }

    [Benchmark(Description = "BB.CRUD: CreateAsync")]
    public Task<BenchProduct> BlazorBase_Create()
        => ProductProvider.CreateAsync(NewProduct());

    [Benchmark(Description = "Raw EF: Update 1 field + SaveChanges")]
    public async Task<int> Raw_PatchSingleField()
    {
        var entity = await CurrentContext.Products.FindAsync(TargetUpdateId);
        if (entity is null)
            return 0;
        entity.Price = 9.99m;
        return await CurrentContext.SaveChangesAsync();
    }

    [Benchmark(Description = "BB.CRUD: PatchAsync 1 field")]
    public Task<BenchProduct> BlazorBase_PatchSingleField()
        => ProductProvider.PatchAsync(TargetUpdateId, SingleFieldPatch);

    [Benchmark(Description = "Raw EF: Update 5 fields + SaveChanges")]
    public async Task<int> Raw_PatchFiveFields()
    {
        var entity = await CurrentContext.Products.FindAsync(TargetUpdateId);
        if (entity is null)
            return 0;
        entity.Price = 9.99m;
        entity.Stock = 50;
        entity.Description = "Updated";
        entity.IsActive = false;
        entity.Name = "Updated Product";
        return await CurrentContext.SaveChangesAsync();
    }

    [Benchmark(Description = "BB.CRUD: PatchAsync 5 fields")]
    public Task<BenchProduct> BlazorBase_PatchFiveFields()
        => ProductProvider.PatchAsync(TargetUpdateId, WidePatch);

    private BenchProduct NewProduct() => new()
    {
        Id = Guid.NewGuid(),
        Name = $"New Product {Guid.NewGuid():N}",
        Price = 19.99m,
        Stock = 10,
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        CategoryId = CategoryIdForCreate
    };
}
