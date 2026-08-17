using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.Querying;

namespace BlazorBase.CRUD.Benchmarks.Scenarios.Http;

[BenchmarkCategory("Http", "Read")]
public class HttpReadBenchmarks : HttpBenchmarkBase
{
    private const int PageSize = 50;

    private Guid TargetId { get; set; }

    protected override void OnAfterGlobalSetup()
        => TargetId = DatabaseFixture.DeterministicGuid(2, RowCount / 2);

    [Benchmark(Baseline = true, Description = "Raw HTTP: GET product by id")]
    public Task<BenchProduct?> Raw_GetById()
        => RawHttpClient.GetFromJsonAsync<BenchProduct>($"/api/raw/products/{TargetId}");

    [Benchmark(Description = "BB.CRUD HTTP: GetByIdAsync")]
    public Task<BenchProduct?> BlazorBase_GetById()
        => ProductProvider.GetByIdAsync(TargetId);

    [Benchmark(Description = "Raw HTTP: POST query (filtered list)")]
    public async Task<RawListResponse<BenchProduct>?> Raw_ListFiltered()
    {
        var response = await RawHttpClient.PostAsJsonAsync("/api/raw/products/query",
            new RawListRequest(OnlyActive: true, NameContains: "000", OrderByName: true, Take: PageSize));
        return await response.Content.ReadFromJsonAsync<RawListResponse<BenchProduct>>();
    }

    [Benchmark(Description = "BB.CRUD HTTP: Query filtered list")]
    public Task<BaseQueryResult<BenchProduct>> BlazorBase_ListFiltered()
        => ProductProvider.Query()
            .Where(p => p.IsActive && p.Name.Contains("000"))
            .OrderBy(p => p.Name)
            .Take(PageSize)
            .ToListAsync();

    [Benchmark(Description = "Raw HTTP: GET count")]
    public Task<int> Raw_Count()
        => RawHttpClient.GetFromJsonAsync<int>("/api/raw/products/count");

    [Benchmark(Description = "BB.CRUD HTTP: GetCountAsync")]
    public Task<int> BlazorBase_Count()
        => ProductProvider.GetCountAsync();
}
