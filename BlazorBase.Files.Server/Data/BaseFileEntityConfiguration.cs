using BlazorBase.Files.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.Files.Server.Data;

/// <summary>
/// EF Core model builder extension that includes <see cref="BaseFile"/> in the host's model.
/// All column constraints are expressed via Data Annotations on <see cref="BaseFile"/> itself;
/// this extension handles only what annotations cannot express.
/// </summary>
public static class BaseFileEntityConfiguration
{
    /// <summary>
    /// Registers <see cref="BaseFile"/> in the model. Call this from the host's
    /// <c>OnModelCreating</c> override (or equivalent) before generating a migration.
    /// </summary>
    /// <param name="modelBuilder">The model builder instance.</param>
    public static ModelBuilder ApplyBaseFileConfiguration(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BaseFile>();
        return modelBuilder;
    }
}
