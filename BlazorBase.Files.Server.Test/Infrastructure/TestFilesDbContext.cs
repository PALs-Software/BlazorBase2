using BlazorBase.Files.Models;
using BlazorBase.Files.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Files.Server.Test.Infrastructure;

/// <summary>Minimal <see cref="DbContext"/> exposing <see cref="BaseFile"/> for the server-side tests.</summary>
public sealed class TestFilesDbContext(DbContextOptions<TestFilesDbContext> options) : DbContext(options)
{
    public DbSet<BaseFile> Files => Set<BaseFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyBaseFileConfiguration();
}
