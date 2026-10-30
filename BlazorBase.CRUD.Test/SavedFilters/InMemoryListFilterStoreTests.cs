using BlazorBase.CRUD.Filtering;
using BlazorBase.CRUD.Models;
using BlazorBase.CRUD.SavedFilters;
using Xunit;

namespace BlazorBase.CRUD.Test.SavedFilters;

/// <summary>
/// The store contract, exercised on the in-memory implementation that shares its snapshot logic with the browser
/// storage one.
/// </summary>
public class InMemoryListFilterStoreTests
{
    private const string ListKey = "Orders";

    private static FilterGroupModel FilterFor(string value) => new()
    {
        Conditions = [new FilterConditionModel { PropertyName = "Name", Operator = FilterOperator.Equals, Value = value }]
    };

    private static SavedListFilterInput Input(string name, string value = "x", Guid? id = null, bool asDefault = false) =>
        new() { Id = id, Name = name, Filter = FilterFor(value), SetAsDefault = asDefault };

    [Fact]
    public async Task Save_CreatesThenOverwritesBySameNameCaseInsensitive()
    {
        var store = new InMemoryListFilterStore();

        var first = await store.SaveAsync(ListKey, Input("Pending", "a"));
        var second = await store.SaveAsync(ListKey, Input("pending", "b"));

        var all = await store.GetAsync(ListKey);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(all);
        Assert.Equal("b", all[0].Filter.Conditions[0].Value);
    }

    [Fact]
    public async Task Save_WithId_RenamesAndKeepsFilter_AndConflictsWithOtherName()
    {
        var store = new InMemoryListFilterStore();
        var a = await store.SaveAsync(ListKey, Input("A"));
        await store.SaveAsync(ListKey, Input("B"));

        var renamed = await store.SaveAsync(ListKey, Input("A2", id: a.Id));

        Assert.Equal(a.Id, renamed.Id);
        await Assert.ThrowsAsync<ListFilterNameConflictException>(() => store.SaveAsync(ListKey, Input("b", id: a.Id)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => store.SaveAsync(ListKey, Input("C", id: Guid.NewGuid())));
    }

    [Fact]
    public async Task SetDefault_KeepsExactlyOneDefault_AndNullClears()
    {
        var store = new InMemoryListFilterStore();
        var a = await store.SaveAsync(ListKey, Input("A", asDefault: true));
        var b = await store.SaveAsync(ListKey, Input("B"));

        Assert.True(await store.SetDefaultAsync(ListKey, b.Id));
        Assert.Equal(["B"], (await store.GetAsync(ListKey)).Where(f => f.IsDefault).Select(f => f.Name));

        Assert.True(await store.SetDefaultAsync(ListKey, null));
        Assert.DoesNotContain(await store.GetAsync(ListKey), f => f.IsDefault);
        Assert.False(await store.SetDefaultAsync(ListKey, Guid.NewGuid()));
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public async Task Delete_RemovesOnlyTheNamedFilter_AndReportsUnknownIds()
    {
        var store = new InMemoryListFilterStore();
        var a = await store.SaveAsync(ListKey, Input("A"));
        await store.SaveAsync(ListKey, Input("B"));

        Assert.True(await store.DeleteAsync(ListKey, a.Id));
        Assert.False(await store.DeleteAsync(ListKey, a.Id));
        Assert.Equal(["B"], (await store.GetAsync(ListKey)).Select(f => f.Name));
    }

    [Fact]
    public async Task Lists_AreIsolatedByKey_AndOrderedByName()
    {
        var store = new InMemoryListFilterStore();
        await store.SaveAsync(ListKey, Input("b"));
        await store.SaveAsync(ListKey, Input("A"));
        await store.SaveAsync("Other", Input("Z"));

        Assert.Equal(["A", "b"], (await store.GetAsync(ListKey)).Select(f => f.Name));
        Assert.Equal(["Z"], (await store.GetAsync("Other")).Select(f => f.Name));
    }

    [Fact]
    public async Task Save_RejectsInvalidNamesAndEnforcesTheQuota()
    {
        var store = new InMemoryListFilterStore();

        await Assert.ThrowsAsync<ListFilterValidationException>(() => store.SaveAsync(ListKey, Input(" ")));

        for (var index = 0; index < ListFilterValidator.MaxFiltersPerList; index++)
            await store.SaveAsync(ListKey, Input($"F{index}"));

        await Assert.ThrowsAsync<ListFilterValidationException>(() => store.SaveAsync(ListKey, Input("One too many")));
    }
}
