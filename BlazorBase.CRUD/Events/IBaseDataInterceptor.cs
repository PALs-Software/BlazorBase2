using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Events;

public interface IBaseDataInterceptor<TModel> where TModel : class
{
    Task<TModel> OnBeforeCreateAsync(TModel model, CancellationToken cancellationToken = default);

    Task OnAfterCreateAsync(TModel model, CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> OnBeforePatchAsync(object id, Dictionary<string, object?> changedFields, CancellationToken cancellationToken = default);

    Task OnAfterPatchAsync(TModel model, CancellationToken cancellationToken = default);

    Task OnBeforeDeleteAsync(object id, TModel model, CancellationToken cancellationToken = default);

    Task OnAfterDeleteAsync(object id, CancellationToken cancellationToken = default);

    Task<BaseQuery> OnBeforeQueryAsync(BaseQuery query, CancellationToken cancellationToken = default);
}
