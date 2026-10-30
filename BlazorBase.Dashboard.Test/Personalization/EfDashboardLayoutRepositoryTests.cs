using BlazorBase.Dashboard.Server;
using BlazorBase.Dashboard.Test.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.Dashboard.Test.Personalization;

public sealed class EfDashboardLayoutRepositoryTests : IDisposable
{
    private const string Key = "/";

    private readonly DashboardLayoutTestDatabase Database = new();
    private readonly EfDashboardLayoutRepository<DashboardLayoutTestDbContext> Repository;

    public EfDashboardLayoutRepositoryTests()
    {
        Repository = new EfDashboardLayoutRepository<DashboardLayoutTestDbContext>(Database);
    }

    public void Dispose() => Database.Dispose();

    private static DashboardLayout Layout(params (string Id, bool Hidden)[] parts) => new()
    {
        Parts = [.. parts.Select((item, index) => new DashboardPartPreference { PartId = item.Id, Hidden = item.Hidden, Order = index })]
    };

    [Fact]
    public async Task Get_ReturnsTheDefaultLayout_WhenNothingIsStored()
    {
        Assert.True((await Repository.GetAsync("alice", Key)).IsDefault);
    }

    [Fact]
    public async Task Save_PersistsOneRowWithUserKeyJsonAndTimestamps()
    {
        await Repository.SaveAsync("alice", Key, Layout(("b", true), ("a", false)));

        await using var context = Database.CreateDbContext();
        var row = await context.Set<SavedDashboardLayout>().SingleAsync();

        Assert.Equal("alice", row.UserId);
        Assert.Equal(Key, row.DashboardKey);
        Assert.Contains("\"partId\":\"b\"", row.LayoutJson);
        Assert.True(row.ModifiedOn >= row.CreatedOn);

        var read = await Repository.GetAsync("alice", Key);
        Assert.Equal(["b", "a"], read.Parts.Select(part => part.PartId));
        Assert.True(read.Parts[0].Hidden);
    }

    [Fact]
    public async Task SavingAgain_ReplacesTheLayout_AndKeepsOneRow()
    {
        await Repository.SaveAsync("alice", Key, Layout(("a", true)));
        await Repository.SaveAsync("alice", Key, Layout(("a", false), ("b", false)));

        await using var context = Database.CreateDbContext();
        Assert.Equal(1, await context.Set<SavedDashboardLayout>().CountAsync());
        Assert.Equal(2, (await Repository.GetAsync("alice", Key)).Parts.Count);
    }

    [Fact]
    public async Task Layouts_AreSeparatePerUserAndPerDashboardKey()
    {
        await Repository.SaveAsync("alice", Key, Layout(("a", true)));
        await Repository.SaveAsync("alice", "/other", Layout(("x", false)));

        Assert.True((await Repository.GetAsync("bob", Key)).IsDefault);
        Assert.Equal(["a"], (await Repository.GetAsync("alice", Key)).Parts.Select(part => part.PartId));
        Assert.Equal(["x"], (await Repository.GetAsync("alice", "/other")).Parts.Select(part => part.PartId));
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheCallersRow()
    {
        await Repository.SaveAsync("alice", Key, Layout(("a", true)));
        await Repository.SaveAsync("bob", Key, Layout(("a", false)));

        Assert.True(await Repository.DeleteAsync("alice", Key));
        Assert.False(await Repository.DeleteAsync("alice", Key));

        Assert.True((await Repository.GetAsync("alice", Key)).IsDefault);
        Assert.Single((await Repository.GetAsync("bob", Key)).Parts);
    }

    [Fact]
    public async Task DeleteAllForUser_RemovesEveryLayoutOfThatUserOnly()
    {
        await Repository.SaveAsync("alice", Key, Layout(("a", true)));
        await Repository.SaveAsync("alice", "/other", Layout(("a", true)));
        await Repository.SaveAsync("bob", Key, Layout(("a", true)));

        Assert.Equal(2, await Repository.DeleteAllForUserAsync("alice"));
        Assert.Equal(0, await Repository.DeleteAllForUserAsync(""));
        Assert.Single((await Repository.GetAsync("bob", Key)).Parts);
    }

    [Fact]
    public async Task InvalidInput_IsRejected_BeforeAnythingIsWritten()
    {
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => Repository.SaveAsync("alice", " ", Layout(("a", false))));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => Repository.SaveAsync("alice", Key, Layout([.. Enumerable.Range(0, 65).Select(index => ($"p{index}", false))])));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => Repository.SaveAsync("alice", Key, Layout(("a", false), ("a", true))));
        await Assert.ThrowsAsync<DashboardLayoutValidationException>(() => Repository.GetAsync("alice", new string('k', DashboardLayoutValidator.MaxKeyLength + 1)));

        await using var context = Database.CreateDbContext();
        Assert.Equal(0, await context.Set<SavedDashboardLayout>().CountAsync());
    }

    [Fact]
    public async Task ADamagedRow_ReadsAsTheDefaultLayout()
    {
        await using (var context = Database.CreateDbContext())
        {
            context.Set<SavedDashboardLayout>().Add(new SavedDashboardLayout { Id = Guid.NewGuid(), UserId = "alice", DashboardKey = Key, LayoutJson = "{{{" });
            await context.SaveChangesAsync();
        }

        Assert.True((await Repository.GetAsync("alice", Key)).IsDefault);
    }

    [Fact]
    public async Task TheUniqueIndexOnUserAndKey_IsEnforcedByTheModel()
    {
        await Repository.SaveAsync("alice", Key, Layout(("a", false)));

        await using var context = Database.CreateDbContext();
        context.Set<SavedDashboardLayout>().Add(new SavedDashboardLayout { Id = Guid.NewGuid(), UserId = "alice", DashboardKey = Key, LayoutJson = "[]" });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task TheModelHasTheTableName_AndNoForeignKeyWithoutTheGenericOverload()
    {
        await using var context = Database.CreateDbContext();
        var entity = context.Model.FindEntityType(typeof(SavedDashboardLayout))!;

        Assert.Equal("SavedDashboardLayouts", entity.GetTableName());
        Assert.Empty(entity.GetForeignKeys());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual([nameof(SavedDashboardLayout.UserId), nameof(SavedDashboardLayout.DashboardKey)]));
    }
}
