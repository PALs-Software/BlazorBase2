using System.Security.Claims;
using BlazorBase.CRUD.Models;

namespace BlazorBase.CRUD.Events;

/// <summary>
/// Passed to action callbacks when a CRUD action is executed.
/// </summary>
public class CrudActionEventArgs<TModel> where TModel : class
{
    public TModel? Item { get; init; }

    public IReadOnlyCollection<TModel> SelectedItems { get; init; } = [];

    public required IServiceProvider ServiceProvider { get; init; }

    public CrudActionContext Context { get; init; }
}

/// <summary>
/// Passed to visibility predicates to determine whether an action should be shown.
/// </summary>
public class CrudActionVisibilityArgs<TModel> where TModel : class
{
    public TModel? Item { get; init; }

    public ClaimsPrincipal? User { get; init; }

    public CrudActionContext Context { get; init; }
}
