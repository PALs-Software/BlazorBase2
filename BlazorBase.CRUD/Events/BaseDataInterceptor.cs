using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Events;

public abstract class BaseDataInterceptor<TModel> : IBaseDataInterceptor<TModel> where TModel : class
{
    public virtual Task<TModel> OnBeforeCreateAsync(TModel model, CancellationToken cancellationToken = default)
        => Task.FromResult(model);

    public virtual Task OnAfterCreateAsync(TModel model, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task<Dictionary<string, object?>> OnBeforePatchAsync(object id, Dictionary<string, object?> changedFields, CancellationToken cancellationToken = default)
        => Task.FromResult(changedFields);

    public virtual Task OnAfterPatchAsync(TModel model, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task OnBeforeDeleteAsync(object id, TModel model, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task OnAfterDeleteAsync(object id, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public virtual Task<BaseQuery> OnBeforeQueryAsync(BaseQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(query);
}
