using BlazorBase.CRUD.Benchmarks.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Benchmarks.Infrastructure;

public class BenchmarkDbContext(DbContextOptions<BenchmarkDbContext> options) : DbContext(options)
{
    public DbSet<BenchCategory> Categories => Set<BenchCategory>();

    public DbSet<BenchProduct> Products => Set<BenchProduct>();

    public DbSet<BenchOrder> Orders => Set<BenchOrder>();

    public DbSet<BenchOrderItem> OrderItems => Set<BenchOrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BenchProduct>()
            .HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BenchProduct>()
            .HasIndex(p => p.CategoryId);

        modelBuilder.Entity<BenchOrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
