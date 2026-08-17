using BlazorBase.User.Server.AccessTokens;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorBase.User.Server.Test.Infrastructure;

/// <summary>
/// One SQLite database, any number of independent application instances over it.
/// </summary>
/// <remarks>
/// SQLite rather than the EF InMemory provider on purpose: InMemory enforces no
/// <c>MaxLength</c>, no unique index and no foreign key, so a suite backed by it cannot say
/// anything about what the database rejects.
///
/// <see cref="CreateInstance"/> builds a fresh service provider each time - and therefore a fresh
/// singleton <c>IAccessTokenCache</c> - while every provider shares the one open connection. That
/// models what a load balancer produces: several processes, separate memory, one database. Tests
/// that only ever use a single instance cannot observe a cache that has gone stale relative to
/// another process, which is exactly the class of bug this harness exists to expose.
/// </remarks>
public sealed class AccessTokenTestHost : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly List<ServiceProvider> Instances = [];
    private readonly IConfiguration Configuration;

    public AccessTokenTestHost(string secretPrefix = "test_")
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AccessTokenSettings:SecretPrefix"] = secretPrefix,
                ["AccessTokenSettings:BruteForceBaseDelayMs"] = "0",
                ["AccessTokenSettings:BruteForceMaxDelayMs"] = "0"
            })
            .Build();

        using var schemaContext = new TestUserDbContext(
            new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(Connection).Options);

        schemaContext.Database.EnsureCreated();
    }

    /// <summary>
    /// Builds an independent application instance: its own service provider, its own token cache,
    /// the same database.
    /// </summary>
    public IServiceProvider CreateInstance()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Configuration);
        services.AddDbContext<TestUserDbContext>(options => options.UseSqlite(Connection));
        services.AddBlazorBaseAccessTokens<TestUserDbContext>(Configuration);

        var provider = services.BuildServiceProvider();
        Instances.Add(provider);
        return provider;
    }

    /// <summary>
    /// Resolves an <see cref="IAccessTokenService"/> from a fresh scope of the given instance,
    /// mirroring the per-request scope a real host would create.
    /// </summary>
    public static IAccessTokenService ResolveService(IServiceProvider instance)
    {
        var scope = instance.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IAccessTokenService>();
    }

    /// <summary>A service on a brand-new instance - the single-process convenience case.</summary>
    public IAccessTokenService CreateService() => ResolveService(CreateInstance());

    public TestUserDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TestUserDbContext>().UseSqlite(Connection).Options);

    public void Dispose()
    {
        foreach (var instance in Instances)
            instance.Dispose();

        Connection.Dispose();
    }
}
