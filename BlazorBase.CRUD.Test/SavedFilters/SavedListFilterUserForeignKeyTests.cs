using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

/// <summary>
/// The opt-in <c>ApplySavedListFilterConfiguration&lt;TUser&gt;</c> overload, checked against a real SQLite database
/// (foreign keys enforced) and the model metadata.
/// </summary>
public sealed class SavedListFilterUserForeignKeyTests : IDisposable
{
    private readonly SqliteConnection Connection = new("DataSource=:memory:");
    private readonly DbContextOptions<SavedFilterUserTestDbContext> Options;

    public SavedListFilterUserForeignKeyTests()
    {
        Connection.Open();
        Options = new DbContextOptionsBuilder<SavedFilterUserTestDbContext>().UseSqlite(Connection).Options;

        using var context = new SavedFilterUserTestDbContext(Options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => Connection.Dispose();

    private static SavedListFilterInput Input(string name) => new()
    {
        Name = name,
        Filter = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = "x" }]
        }
    };

    private async Task<EfListFilterRepository<SavedFilterUserTestDbContext>> CreateRepositoryAsync(params string[] userIds)
    {
        await using var context = new SavedFilterUserTestDbContext(Options);
        context.AddRange(userIds.Select(id => new SavedFilterTestUser { Id = id }));
        await context.SaveChangesAsync();

        return new EfListFilterRepository<SavedFilterUserTestDbContext>(new Factory(Options));
    }

    [Fact]
    public void Model_HasACascadingRequiredForeignKeyFromUserIdToTheUser()
    {
        using var context = new SavedFilterUserTestDbContext(Options);
        var foreignKey = Assert.Single(context.Model.FindEntityType(typeof(SavedListFilter))!.GetForeignKeys());

        Assert.Equal(typeof(SavedFilterTestUser), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(SavedListFilter.UserId)], foreignKey.Properties.Select(property => property.Name));
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.True(foreignKey.IsRequired);
    }

    [Fact]
    public void NonGenericConfiguration_AddsNoForeignKey()
    {
        using var database = new SavedFilterTestDatabase();
        using var context = database.CreateDbContext();

        Assert.Empty(context.Model.FindEntityType(typeof(SavedListFilter))!.GetForeignKeys());
    }

    [Fact]
    public async Task DeletingTheUser_DeletesTheirFiltersAndKeepsOthers()
    {
        var repository = await CreateRepositoryAsync("alice", "bob");
        await repository.SaveAsync("alice", "Orders", Input("A1"));
        await repository.SaveAsync("alice", "Orders@/other", Input("A2"));
        await repository.SaveAsync("bob", "Orders", Input("B1"));

        await using (var context = new SavedFilterUserTestDbContext(Options))
        {
            context.Remove(await context.Set<SavedFilterTestUser>().SingleAsync(user => user.Id == "alice"));
            await context.SaveChangesAsync();
        }

        await using var check = new SavedFilterUserTestDbContext(Options);
        var rows = await check.Set<SavedListFilter>().ToListAsync();

        var remaining = Assert.Single(rows);
        Assert.Equal("bob", remaining.UserId);
    }

    [Fact]
    public async Task SavingForAnUnknownUser_IsRejectedByTheDatabase()
    {
        var repository = await CreateRepositoryAsync("alice");

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveAsync("ghost", "Orders", Input("A1")));
    }

    private sealed class Factory(DbContextOptions<SavedFilterUserTestDbContext> options) : IDbContextFactory<SavedFilterUserTestDbContext>
    {
        public SavedFilterUserTestDbContext CreateDbContext() => new(options);
    }
}
