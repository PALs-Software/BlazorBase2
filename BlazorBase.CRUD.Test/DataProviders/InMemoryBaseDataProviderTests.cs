using BlazorBase.CRUD.DataProviders;
using BlazorBase.CRUD.Test.Infrastructure.TestEntities;
using Xunit;

namespace BlazorBase.CRUD.Test.DataProviders;

public class InMemoryBaseDataProviderTests
{
    private class KeylessItem
    {
        public string Name { get; set; } = string.Empty;
    }

    [Fact]
    public async Task GetListAsync_ReturnsCurrentAccessorSnapshot()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid(), Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetListAsync(new BlazorBase.CRUD.Models.BaseQuery());

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("A", Assert.Single(result.Items).Name);
    }

    [Fact]
    public async Task GetByIdAsync_FindsEntityByReflectedIdProperty()
    {
        var id = Guid.NewGuid();
        var items = new List<TestProduct> { new() { Id = id, Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetByIdAsync(id);

        Assert.Equal("A", result!.Name);
    }

    [Fact]
    public async Task GetByIdAsync_MissingId_ReturnsNull()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid(), Name = "A" } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var result = await provider.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_ModelWithoutIdProperty_ReturnsNull()
    {
        var items = new List<KeylessItem> { new() { Name = "A" } };
        var provider = new InMemoryBaseDataProvider<KeylessItem>(() => items);

        var result = await provider.GetByIdAsync("anything");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCountAsync_MatchesListLength()
    {
        var items = new List<TestProduct> { new() { Id = Guid.NewGuid() }, new() { Id = Guid.NewGuid() } };
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => items);

        var count = await provider.GetCountAsync();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task CreateAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.CreateAsync(new TestProduct()));
    }

    [Fact]
    public async Task PatchAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => provider.PatchAsync(Guid.NewGuid(), new Dictionary<string, object?>()));
    }

    [Fact]
    public async Task DeleteAsync_ThrowsNotSupported()
    {
        var provider = new InMemoryBaseDataProvider<TestProduct>(() => []);

        await Assert.ThrowsAsync<NotSupportedException>(() => provider.DeleteAsync(Guid.NewGuid()));
    }
}
