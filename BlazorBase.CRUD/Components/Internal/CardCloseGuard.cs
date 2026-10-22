namespace BlazorBase.CRUD.Components.Internal;

/// <summary>
/// The hook through which a list asks the card in its dialog about unsaved changes and asks it to close, so that a
/// navigation the browser performs (back, forward, a link) goes through the same confirmation as the close button.
/// </summary>
/// <remarks>
/// The dialog hands one instance to the card it hosts as a cascading value; the outermost card registers itself.
/// Without a registered card the guard reports no unsaved changes and never closes anything.
/// </remarks>
internal sealed class CardCloseGuard
{
    private object? Owner;
    private Func<bool>? HasChangesReader;
    private Func<Task<bool>>? CloseRequester;

    /// <summary>
    /// Whether the card holds changes that were not saved.
    /// </summary>
    public bool HasUnsavedChanges => HasChangesReader?.Invoke() ?? false;

    /// <summary>
    /// Registers the card that answers for this guard; the first registration wins, so a card nested inside the
    /// card does not take over.
    /// </summary>
    public void Register(object owner, Func<bool> hasUnsavedChanges, Func<Task<bool>> requestClose)
    {
        if (Owner is not null)
            return;

        Owner = owner;
        HasChangesReader = hasUnsavedChanges;
        CloseRequester = requestClose;
    }

    /// <summary>
    /// Removes the registration made by the card that is going away.
    /// </summary>
    public void Unregister(object owner)
    {
        if (!ReferenceEquals(Owner, owner))
            return;

        Owner = null;
        HasChangesReader = null;
        CloseRequester = null;
    }

    /// <summary>
    /// Asks the card to close the way its close button does: with the confirmation when there are unsaved changes.
    /// </summary>
    /// <returns>Whether the card closed (or was asked to, because the user chose to discard).</returns>
    public Task<bool> RequestCloseAsync() => CloseRequester?.Invoke() ?? Task.FromResult(true);
}
