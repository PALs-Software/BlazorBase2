using System.Linq.Expressions;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Test.Endpoints;

/// <summary>
/// Satisfies <see cref="IBaseDataProvider{TModel}"/> for every [BaseCrud] entity that an
/// assembly-scan pulls in incidentally so route metadata inference succeeds for all of them —
/// none of these routes are ever called. Only <see cref="ConfiguratorTestEntity"/>'s own provider
/// (<see cref="RecordingDataProvider"/>) is meant to be exercised.
/// </summary>
internal sealed class NoOpDataProvider<T> : IBaseDataProvider<T> where T : class
{
    public Task<BaseQueryResult<T>> GetListAsync(BaseQuery query, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<T?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<int> GetCountAsync(Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<T> CreateAsync(T model, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<T> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
