using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Test.SavedFilters;

/// <summary>
/// A real in-memory SQLite database with the saved-filter model, handing out a fresh context per call like the
/// host's context factory does.
/// </summary>
public sealed class SavedFilterTestDatabase : IDbContextFactory<SavedFilterTestDbContext>, IDisposable
{
    private readonly SqliteConnection Connection = new("DataSource=:memory:");
    private readonly DbContextOptions<SavedFilterTestDbContext> Options;

    public SavedFilterTestDatabase()
    {
        Connection.Open();
        Options = new DbContextOptionsBuilder<SavedFilterTestDbContext>().UseSqlite(Connection).Options;

        using var context = CreateDbContext();
        context.Database.EnsureCreated();
    }

    public SavedFilterTestDbContext CreateDbContext() => new(Options);

    public void Dispose() => Connection.Dispose();
}
