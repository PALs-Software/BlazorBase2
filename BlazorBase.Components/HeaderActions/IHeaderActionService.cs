namespace BlazorBase.Components.HeaderActions;

/// <summary>
/// The header actions of the current scope: the items registered in dependency injection plus those added or
/// removed at run time.
/// </summary>
public interface IHeaderActionService
{
    /// <summary>The current items ordered by <see cref="HeaderActionItem.Order"/> and then <see cref="HeaderActionItem.Id"/>.</summary>
    IReadOnlyList<HeaderActionItem> Items { get; }

    /// <summary>Raised after <see cref="Register"/>, <see cref="Unregister"/> or <see cref="Refresh"/>.</summary>
    event Action? Changed;

    /// <summary>Adds the item or replaces the one with the same id.</summary>
    void Register(HeaderActionItem item);

    /// <summary>Removes the item; false when there was none with that id.</summary>
    bool Unregister(string id);

    /// <summary>Asks the header to re-evaluate visibility and pressed state, e.g. after a panel opened.</summary>
    void Refresh();
}
