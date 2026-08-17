using BlazorBase.Files.Server.Services;
using BlazorBase.Files.Server.Test.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace BlazorBase.Files.Server.Test.Services;

public sealed class BlazorBaseFilesServerServiceCollectionExtensionsTests : IDisposable
{
    private readonly SqliteConnection Connection;

    public BlazorBaseFilesServerServiceCollectionExtensionsTests()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
    }

    [Fact]
    public void AddBlazorBaseFilesServer_BridgesBaseDbContext_FromConcreteHostContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestFilesDbContext>(options => options.UseSqlite(Connection));
        services.AddBlazorBaseFilesServer<TestFilesDbContext>();

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var fileAccessAuthorizer = scope.ServiceProvider.GetRequiredService<IFileAccessAuthorizer>();
        Assert.IsType<OwnerScopedFileAccessAuthorizer>(fileAccessAuthorizer);

        var concreteDbContext = scope.ServiceProvider.GetRequiredService<TestFilesDbContext>();
        var baseDbContext = scope.ServiceProvider.GetRequiredService<DbContext>();
        Assert.Same(concreteDbContext, baseDbContext);
    }

    public void Dispose()
    {
        Connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
