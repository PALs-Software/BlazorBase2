namespace BlazorBase.CRUD.Components;

/// <summary>
/// Implemented by list-part components whose pending Add/Remove/Update operations
/// must be flushed to the server after the parent entity has been saved.
/// </summary>
public interface IListPartFlushable
{
    /// <summary>
    /// Indicates whether the list part holds unsaved Add/Remove/Update operations.
    /// </summary>
    bool HasPendingChanges { get; }

    Task FlushAsync(object parentEntity, CancellationToken cancellationToken = default);
}
