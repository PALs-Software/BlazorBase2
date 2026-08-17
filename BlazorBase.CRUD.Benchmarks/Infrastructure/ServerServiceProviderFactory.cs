using BlazorBase.CRUD.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.CRUD.Benchmarks.Infrastructure;

public static class ServerServiceProviderFactory
{
    public static ServiceProvider Build(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddBlazorBaseCrud();
        services.AddBaseDbContext<BenchmarkDbContext>(options => options.UseSqlite(connectionString));
        services.AddBlazorBaseCrudServer<BenchmarkDbContext>(typeof(BenchmarkDbContext).Assembly);

        return services.BuildServiceProvider(validateScopes: true);
    }
}
