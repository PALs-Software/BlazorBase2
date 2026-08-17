using System.Net.Http.Json;
using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Extensions;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[MemoryDiagnoser]
[CategoriesColumn]
public abstract class HttpBenchmarkBase
{
    protected string DatabasePath { get; private set; } = string.Empty;

    protected BenchmarkWebHost WebHost { get; private set; } = default!;

    protected ServiceProvider ClientServices { get; private set; } = default!;

    protected IServiceScope CurrentScope { get; private set; } = default!;

    protected IBaseDataProvider<BenchProduct> ProductProvider { get; private set; } = default!;

    protected IBaseDataProvider<BenchOrder> OrderProvider { get; private set; } = default!;

    protected HttpClient RawHttpClient { get; private set; } = default!;

    [Params(100, 10_000)]
    public int RowCount { get; set; }

    protected int OrderCount => Math.Max(10, RowCount / 10);

    [GlobalSetup]
    public void GlobalSetup()
    {
        GlobalSetupCore();
        OnAfterGlobalSetup();
    }

    /// <summary>
    /// Override in derived classes to perform scenario-specific setup after
    /// the host has started and the database is seeded. Do NOT add
    /// [GlobalSetup] to the override.
    /// </summary>
    protected virtual void OnAfterGlobalSetup()
    {
    }

    private void GlobalSetupCore()
    {
        DatabasePath = DatabaseFixture.CreateDatabaseFile(GetType().Name + "-http");
        var connectionString = DatabaseFixture.BuildConnectionString(DatabasePath);

        using (var seedServices = ServerServiceProviderFactory.Build(connectionString))
        using (var seedScope = seedServices.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            DatabaseFixture.CreateAndSeed(ctx, RowCount, OrderCount);
        }

        WebHost = BenchmarkWebHost.StartAsync(connectionString).GetAwaiter().GetResult();

        var clientServices = new ServiceCollection();
        clientServices.AddLogging();
        clientServices.AddBlazorBaseCrud();
        clientServices.AddBlazorBaseCrudClient(
            client => client.BaseAddress = new Uri(WebHost.BaseAddress, "api/base/"),
            typeof(BenchmarkDbContext).Assembly);
        ClientServices = clientServices.BuildServiceProvider();

        RawHttpClient = new HttpClient { BaseAddress = WebHost.BaseAddress };

        WarmUp();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        RawHttpClient.Dispose();
        ClientServices.Dispose();
        WebHost.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (File.Exists(DatabasePath))
        {
            try { File.Delete(DatabasePath); } catch { }
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        CurrentScope = ClientServices.CreateScope();
        ProductProvider = CurrentScope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchProduct>>();
        OrderProvider = CurrentScope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchOrder>>();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CurrentScope.Dispose();
    }

    protected virtual void WarmUp()
    {
        using var scope = ClientServices.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchProduct>>();
        provider.GetListAsync(new BaseQuery { Take = 1 }).GetAwaiter().GetResult();
        provider.GetCountAsync().GetAwaiter().GetResult();

        RawHttpClient.GetAsync("/api/raw/products/count").GetAwaiter().GetResult();
        RawHttpClient.PostAsJsonAsync("/api/raw/products/query",
            new RawListRequest(null, null, null, 1)).GetAwaiter().GetResult();
    }
}
