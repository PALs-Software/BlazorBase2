using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Microsoft.EntityFrameworkCore;

namespace BlazorBase.CRUD.Test.Infrastructure;

public class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
{
    public DbSet<TestProduct> Products => Set<TestProduct>();

    public DbSet<TestCategory> Categories => Set<TestCategory>();

    public DbSet<TestReview> Reviews => Set<TestReview>();

    public DbSet<TestOrder> Orders => Set<TestOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestProduct>()
            .HasMany(product => product.Reviews)
            .WithOne()
            .HasForeignKey(review => review.ProductId);
    }
}
