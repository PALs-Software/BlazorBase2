using BlazorBase.CRUD.Events;

namespace BlazorBase.CRUD.Models;

/// <summary>
/// Configuration for rendering a dynamic component when a CRUD action is triggered.
/// </summary>
public class CrudActionComponentConfig<TModel> where TModel : class
{
    public required Type ComponentType { get; set; }

    public Dictionary<string, object?>? Parameters { get; set; }

    public Func<CrudActionEventArgs<TModel>, Task>? OnComponentClosed { get; set; }
}
