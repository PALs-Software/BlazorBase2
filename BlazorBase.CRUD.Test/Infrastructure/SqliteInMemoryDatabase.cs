using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BlazorBase.CRUD.Test.Infrastructure;

/// <summary>
/// An isolated, real SQLite database living entirely in memory. The connection is kept
/// open for the lifetime of the instance so the schema and data persist across the
/// multiple <see cref="TestDbContext"/> instances a single test creates (mirroring the
/// library's "fresh DbContext per operation" pattern). Dispose closes the connection and
/// discards the database.
/// </summary>
public sealed class SqliteInMemoryDatabase : IDisposable
{
    private readonly SqliteConnection Connection;
    private readonly DbContextOptions<TestDbContext> Options;

    public SqliteInMemoryDatabase(params IInterceptor[] interceptors)
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var builder = new DbContextOptionsBuilder<TestDbContext>().UseSqlite(Connection);

        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);

        Options = builder.Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public TestDbContext CreateContext()
    {
        return new TestDbContext(Options);
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}
