using BenchmarkDotNet.Attributes;
using BlazorBase.CRUD.Benchmarks.Entities;
using BlazorBase.CRUD.Benchmarks.Infrastructure;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.Benchmarks.Scenarios;

[MemoryDiagnoser]
[CategoriesColumn]
public abstract class ServerBenchmarkBase
{
    protected string DatabasePath { get; private set; } = string.Empty;

    protected string ConnectionString { get; private set; } = string.Empty;

    protected ServiceProvider RootServices { get; private set; } = default!;

    protected IServiceScope CurrentScope { get; private set; } = default!;

    protected BenchmarkDbContext CurrentContext { get; private set; } = default!;

    protected IBaseDataProvider<BenchProduct> ProductProvider { get; private set; } = default!;

    protected IBaseDataProvider<BenchOrder> OrderProvider { get; private set; } = default!;

    protected IBaseDataProvider<BenchCategory> CategoryProvider { get; private set; } = default!;

    [Params(100, 10_000)]
    public int RowCount { get; set; }

    protected int OrderCount => Math.Max(10, RowCount / 10);

    [GlobalSetup]
    public void GlobalSetup()
    {
        DatabasePath = DatabaseFixture.CreateDatabaseFile(GetType().Name);
        ConnectionString = DatabaseFixture.BuildConnectionString(DatabasePath);
        RootServices = ServerServiceProviderFactory.Build(ConnectionString);

        using (var seedScope = RootServices.CreateScope())
        {
            var ctx = seedScope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
            DatabaseFixture.CreateAndSeed(ctx, RowCount, OrderCount);
        }

        WarmUp();
        OnAfterGlobalSetup();
    }

    /// <summary>
    /// Override in derived classes to perform scenario-specific setup
    /// (e.g. resolving target IDs) after the database is seeded and warmed up.
    /// Do NOT add [GlobalSetup] to the override — BenchmarkDotNet only allows
    /// one untargeted [GlobalSetup] per class.
    /// </summary>
    protected virtual void OnAfterGlobalSetup()
    {
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        RootServices.Dispose();
        if (File.Exists(DatabasePath))
        {
            try { File.Delete(DatabasePath); } catch { }
        }
    }

    [IterationSetup]
    public void IterationSetup()
    {
        CurrentScope = RootServices.CreateScope();
        CurrentContext = CurrentScope.ServiceProvider.GetRequiredService<BenchmarkDbContext>();
        ProductProvider = CurrentScope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchProduct>>();
        OrderProvider = CurrentScope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchOrder>>();
        CategoryProvider = CurrentScope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchCategory>>();
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        CurrentScope.Dispose();
    }

    /// <summary>
    /// Runs each benchmarked path once during GlobalSetup so projection caches
    /// and expression decomposer caches are warm before measurement starts.
    /// Override in derived classes for scenario-specific warmups.
    /// </summary>
    protected virtual void WarmUp()
    {
        using var scope = RootServices.CreateScope();
        var productProvider = scope.ServiceProvider.GetRequiredService<IBaseDataProvider<BenchProduct>>();
        productProvider.GetListAsync(new BaseQuery { Take = 1 }).GetAwaiter().GetResult();
        productProvider.GetCountAsync().GetAwaiter().GetResult();
    }
}
