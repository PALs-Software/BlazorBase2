namespace BlazorBase.CRUD.Events;

/// <summary>
/// Event args for property value changes on BaseCard. Set Cancel to true to prevent the change.
/// </summary>
public class PropertyChangedEventArgs<TModel>
{
    public required TModel Model { get; init; }

    public required string PropertyName { get; init; }

    public object? OldValue { get; init; }

    public object? NewValue { get; init; }

    public bool Cancel { get; set; }
}
