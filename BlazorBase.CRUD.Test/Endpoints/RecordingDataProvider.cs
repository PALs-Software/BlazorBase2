using System.Linq.Expressions;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// In-memory <see cref="IBaseDataProvider{TModel}"/> seeded with two rows owned by different
/// principals, so a test can prove a <see cref="Endpoints.BaseEndpointOptions{TEntity}.UserFilter"/>
/// actually reached the query rather than merely being set on the options object.
/// </summary>
internal sealed class RecordingDataProvider : IBaseDataProvider<ConfiguratorTestEntity>
{
    public List<ConfiguratorTestEntity> Items { get; } =
    [
        new() { Id = 1, Owner = "alice" },
        new() { Id = 2, Owner = "bob" },
    ];

    public Task<BaseQueryResult<ConfiguratorTestEntity>> GetListAsync(
        BaseQuery query,
        Expression<Func<ConfiguratorTestEntity, bool>>? scopeFilter = null,
        CancellationToken cancellationToken = default)
    {
        var query1 = Items.AsQueryable();

        if (scopeFilter is not null)
            query1 = query1.Where(scopeFilter);

        var items = query1.ToList();
        return Task.FromResult(new BaseQueryResult<ConfiguratorTestEntity> { Items = items, TotalCount = items.Count });
    }

    public Task<ConfiguratorTestEntity?> GetByIdAsync(
        object id,
        IEnumerable<string>? select = null,
        Expression<Func<ConfiguratorTestEntity, bool>>? scopeFilter = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by BaseEndpointMapperConfiguratorTests.");

    public Task<int> GetCountAsync(
        Expression<Func<ConfiguratorTestEntity, bool>>? scopeFilter = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by BaseEndpointMapperConfiguratorTests.");

    public Task<ConfiguratorTestEntity> CreateAsync(ConfiguratorTestEntity model, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by BaseEndpointMapperConfiguratorTests.");

    public Task<ConfiguratorTestEntity> PatchAsync(
        object id,
        Dictionary<string, object?> changedFields,
        string? concurrencyStamp = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by BaseEndpointMapperConfiguratorTests.");

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by BaseEndpointMapperConfiguratorTests.");
}
