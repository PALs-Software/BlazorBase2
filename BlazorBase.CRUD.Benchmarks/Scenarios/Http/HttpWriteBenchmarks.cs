using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;

namespace BlazorBase.CRUD.Benchmarks.Scenarios.Http;

[BenchmarkCategory("Http", "Write")]
public class HttpWriteBenchmarks : HttpBenchmarkBase
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

    [Benchmark(Baseline = true, Description = "Raw HTTP: POST product")]
    public async Task<BenchProduct?> Raw_Create()
    {
        var response = await RawHttpClient.PostAsJsonAsync("/api/raw/products/", NewProduct());
        return await response.Content.ReadFromJsonAsync<BenchProduct>();
    }

    [Benchmark(Description = "BB.CRUD HTTP: CreateAsync")]
    public Task<BenchProduct> BlazorBase_Create()
        => ProductProvider.CreateAsync(NewProduct());

    [Benchmark(Description = "Raw HTTP: PATCH 1 field")]
    public async Task<BenchProduct?> Raw_PatchSingleField()
    {
        var response = await RawHttpClient.PatchAsJsonAsync(
            $"/api/raw/products/{TargetUpdateId}",
            new RawPatch(Price: 9.99m, Stock: null, Name: null, Description: null, IsActive: null));
        return await response.Content.ReadFromJsonAsync<BenchProduct>();
    }

    [Benchmark(Description = "BB.CRUD HTTP: PatchAsync 1 field")]
    public Task<BenchProduct> BlazorBase_PatchSingleField()
        => ProductProvider.PatchAsync(TargetUpdateId, SingleFieldPatch);

    [Benchmark(Description = "Raw HTTP: PATCH 5 fields")]
    public async Task<BenchProduct?> Raw_PatchFiveFields()
    {
        var response = await RawHttpClient.PatchAsJsonAsync(
            $"/api/raw/products/{TargetUpdateId}",
            new RawPatch(Price: 9.99m, Stock: 50, Name: "Updated", Description: "Updated", IsActive: false));
        return await response.Content.ReadFromJsonAsync<BenchProduct>();
    }

    [Benchmark(Description = "BB.CRUD HTTP: PatchAsync 5 fields")]
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
