using System.Linq.Expressions;
using System.Reflection;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.DataProviders;

/// <summary>
/// Read-only <see cref="IBaseDataProvider{TModel}"/> over an in-memory list supplied by a live
/// delegate, for hosts whose own caller already filtered, sorted and computed the full list to
/// display (e.g. precomputed statistics) — no query translation is attempted here.
/// </summary>
public class InMemoryBaseDataProvider<T>(
    Func<IReadOnlyList<T>> itemsAccessor
) : IBaseDataProvider<T>
    where T : class
{
    #region Injects
    private readonly Func<IReadOnlyList<T>> ItemsAccessor = itemsAccessor ?? throw new ArgumentNullException(nameof(itemsAccessor));
    #endregion

    private static readonly PropertyInfo? IdProperty = typeof(T).GetProperty("Id");

    public Task<BaseQueryResult<T>> GetListAsync(BaseQuery query, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        var items = ItemsAccessor();
        return Task.FromResult(new BaseQueryResult<T> { Items = [.. items], TotalCount = items.Count });
    }

    public Task<T?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
    {
        if (IdProperty is null)
            return Task.FromResult<T?>(null);

        var match = ItemsAccessor().FirstOrDefault(item => Equals(IdProperty.GetValue(item), id));
        return Task.FromResult(match);
    }

    public Task<int> GetCountAsync(Expression<Func<T, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ItemsAccessor().Count);

    public Task<T> CreateAsync(T model, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support creating entries.");

    public Task<T> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support patching entries.");

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"{nameof(InMemoryBaseDataProvider<T>)} is read-only and does not support deleting entries.");
}
