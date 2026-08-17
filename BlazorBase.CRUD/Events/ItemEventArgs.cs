namespace BlazorBase.CRUD.Events;

/// <summary>
/// Event args for item-level operations (create, delete, add, remove). Set Cancel to true to abort.
/// </summary>
public class ItemEventArgs<TModel>
{
    public required TModel Item { get; init; }

    public bool Cancel { get; set; }
}
