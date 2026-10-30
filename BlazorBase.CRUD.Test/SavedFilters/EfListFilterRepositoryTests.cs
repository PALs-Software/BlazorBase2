using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using BlazorBase.CRUD.SavedFilters.Server;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

public sealed class EfListFilterRepositoryTests : IDisposable
{
    private const string ListKey = "Orders";

    private readonly SavedFilterTestDatabase Database = new();
    private readonly EfListFilterRepository<SavedFilterTestDbContext> Repository;

    public EfListFilterRepositoryTests()
    {
        Repository = new EfListFilterRepository<SavedFilterTestDbContext>(Database);
    }

    public void Dispose() => Database.Dispose();

    private static SavedListFilterInput Input(string name, string value = "x", Guid? id = null, bool asDefault = false) => new()
    {
        Id = id,
        Name = name,
        SetAsDefault = asDefault,
        Filter = new FilterGroupModel
        {
            Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = value }]
        }
    };

    [Fact]
    public async Task Save_PersistsRowWithUserListNameJsonAndTimestamps()
    {
        var saved = await Repository.SaveAsync("alice", ListKey, Input(" Pending ", "p", asDefault: true));

        await using var context = Database.CreateDbContext();
        var row = await context.Set<SavedListFilter>().SingleAsync();

        Assert.Equal(saved.Id, row.Id);
        Assert.Equal("alice", row.UserId);
        Assert.Equal(ListKey, row.ListKey);
        Assert.Equal("Pending", row.Name);
        Assert.True(row.IsDefault);
        Assert.Contains("\"Equals\"", row.FilterJson);
        Assert.True(row.ModifiedOn >= row.CreatedOn);
        Assert.Equal("p", (await Repository.GetAsync("alice", ListKey))[0].Filter.Conditions[0].Value);
    }

    [Fact]
    public async Task Save_OverwritesByNameRenamesByIdAndRejectsConflicts()
    {
        var a = await Repository.SaveAsync("alice", ListKey, Input("A", "1"));
        var again = await Repository.SaveAsync("alice", ListKey, Input("a", "2"));
        await Repository.SaveAsync("alice", ListKey, Input("B"));
        var renamed = await Repository.SaveAsync("alice", ListKey, Input("A2", id: a.Id));

        Assert.Equal(a.Id, again.Id);
        Assert.Equal(a.Id, renamed.Id);
        await Assert.ThrowsAsync<ListFilterNameConflictException>(() => Repository.SaveAsync("alice", ListKey, Input("b", id: a.Id)));
        Assert.Equal(["A2", "B"], (await Repository.GetAsync("alice", ListKey)).Select(f => f.Name));
    }

    [Fact]
    public async Task SetDefault_LeavesExactlyOneDefaultPerUserAndList()
    {
        var a = await Repository.SaveAsync("alice", ListKey, Input("A", asDefault: true));
        var b = await Repository.SaveAsync("alice", ListKey, Input("B"));
        var other = await Repository.SaveAsync("alice", "Other", Input("O", asDefault: true));

        Assert.True(await Repository.SetDefaultAsync("alice", ListKey, b.Id));

        var defaults = (await Repository.GetAsync("alice", ListKey)).Where(f => f.IsDefault).Select(f => f.Name);
        Assert.Equal(["B"], defaults);
        Assert.True((await Repository.GetAsync("alice", "Other")).Single().IsDefault);

        Assert.True(await Repository.SetDefaultAsync("alice", ListKey, null));
        Assert.DoesNotContain(await Repository.GetAsync("alice", ListKey), f => f.IsDefault);
        Assert.NotEqual(a.Id, other.Id);
    }

    [Fact]
    public async Task Delete_RemovesTheRow()
    {
        var a = await Repository.SaveAsync("alice", ListKey, Input("A"));

        Assert.True(await Repository.DeleteAsync("alice", ListKey, a.Id));
        Assert.False(await Repository.DeleteAsync("alice", ListKey, a.Id));

        await using var context = Database.CreateDbContext();
        Assert.Empty(context.Set<SavedListFilter>());
    }

    [Fact]
    public async Task AnotherUser_CannotReadChangeDefaultOrDeleteTheFilters()
    {
        var mine = await Repository.SaveAsync("alice", ListKey, Input("Mine", "secret", asDefault: true));

        Assert.Empty(await Repository.GetAsync("bob", ListKey));
        Assert.False(await Repository.DeleteAsync("bob", ListKey, mine.Id));
        Assert.False(await Repository.SetDefaultAsync("bob", ListKey, mine.Id));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Repository.SaveAsync("bob", ListKey, Input("Hijack", id: mine.Id)));

        var bobs = await Repository.SaveAsync("bob", ListKey, Input("Mine", "bobs"));
        Assert.NotEqual(mine.Id, bobs.Id);

        var alices = Assert.Single(await Repository.GetAsync("alice", ListKey));
        Assert.Equal("secret", alices.Filter.Conditions[0].Value);
        Assert.True(alices.IsDefault);
        Assert.False(Assert.Single(await Repository.GetAsync("bob", ListKey)).IsDefault);
    }

    [Fact]
    public async Task DeleteAllForUser_RemovesEveryListOfThatUserOnly()
    {
        await Repository.SaveAsync("alice", ListKey, Input("A1"));
        await Repository.SaveAsync("alice", "Other", Input("A2", asDefault: true));
        await Repository.SaveAsync("bob", ListKey, Input("B1"));

        Assert.Equal(2, await Repository.DeleteAllForUserAsync("alice"));
        Assert.Equal(0, await Repository.DeleteAllForUserAsync("alice"));
        Assert.Equal(0, await Repository.DeleteAllForUserAsync(""));

        Assert.Empty(await Repository.GetAsync("alice", ListKey));
        Assert.Empty(await Repository.GetAsync("alice", "Other"));
        Assert.Equal("B1", Assert.Single(await Repository.GetAsync("bob", ListKey)).Name);
    }

    [Fact]
    public async Task Save_EnforcesTheQuotaPerUserAndList()
    {
        for (var index = 0; index < ListFilterValidator.MaxFiltersPerList; index++)
            await Repository.SaveAsync("alice", ListKey, Input($"F{index}"));

        await Assert.ThrowsAsync<ListFilterValidationException>(() => Repository.SaveAsync("alice", ListKey, Input("extra")));
        await Repository.SaveAsync("bob", ListKey, Input("extra"));
        await Repository.SaveAsync("alice", "Other", Input("extra"));
    }
}
