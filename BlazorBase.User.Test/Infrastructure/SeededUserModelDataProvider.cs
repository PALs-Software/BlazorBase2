using System.Linq.Expressions;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using BlazorBase.User.Models;

namespace BlazorBase.User.Test.Infrastructure;

/// <summary>
/// In-memory <see cref="IBaseDataProvider{UserModel}"/> seeded with a fixed user set so the generic
/// user-management UI can be exercised end to end (list rendering, create, edit, delete) without a
/// backend.
/// </summary>
public sealed class SeededUserModelDataProvider : IBaseDataProvider<UserModel>
{
    private readonly List<UserModel> Users;

    public SeededUserModelDataProvider(params UserModel[] users)
    {
        Users = [.. users];
    }

    public Task<BaseQueryResult<UserModel>> GetListAsync(BaseQuery query, Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new BaseQueryResult<UserModel> { Items = [.. Users], TotalCount = Users.Count });

    public Task<UserModel?> GetByIdAsync(object id, IEnumerable<string>? select = null, Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(user => user.Id == id?.ToString()));

    public Task<int> GetCountAsync(Expression<Func<UserModel, bool>>? scopeFilter = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.Count);

    public Task<UserModel> CreateAsync(UserModel model, CancellationToken cancellationToken = default)
    {
        Users.Add(model);
        return Task.FromResult(model);
    }

    public Task<UserModel> PatchAsync(object id, Dictionary<string, object?> changedFields, string? concurrencyStamp = null, CancellationToken cancellationToken = default)
        => Task.FromResult(Users.FirstOrDefault(user => user.Id == id?.ToString()) ?? new UserModel());

    public Task DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        Users.RemoveAll(user => user.Id == id?.ToString());
        return Task.CompletedTask;
    }
}
