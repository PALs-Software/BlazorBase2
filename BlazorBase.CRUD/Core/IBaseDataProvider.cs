using System.Linq.Expressions;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Core;

/// <summary>
/// Abstraction for data access — implemented by DbContext (server) and HTTP (client) providers.
/// </summary>
public interface IBaseDataProvider<TModel> where TModel : class
{
    Task<BaseQueryResult<TModel>> GetListAsync(BaseQuery query, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default);

    Task<TModel?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default);

    Task<int> GetCountAsync(Expression<Func<TModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default);

    Task<TModel> CreateAsync(TModel model, CancellationToken cancellationToken = default);

    Task<TModel> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(object id, CancellationToken cancellationToken = default);
}
