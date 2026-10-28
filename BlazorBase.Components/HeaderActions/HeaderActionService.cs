namespace BlazorBase.Components.HeaderActions;

/// <summary>Default <see cref="IHeaderActionService"/>; scoped, so on a server every circuit has its own list.</summary>
public sealed class HeaderActionService(IEnumerable<HeaderActionItem> registered) : IHeaderActionService
{
    #region Members

    private readonly Dictionary<string, HeaderActionItem> ItemsById = registered
        .GroupBy(item => item.Id, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

    #endregion

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<HeaderActionItem> Items => HeaderActionOrdering.Order(ItemsById.Values);

    /// <inheritdoc />
    public void Register(HeaderActionItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemsById[item.Id] = item;
        Changed?.Invoke();
    }

    /// <inheritdoc />
    public bool Unregister(string id)
    {
        var removed = ItemsById.Remove(id);
        if (removed)
            Changed?.Invoke();

        return removed;
    }

    /// <inheritdoc />
    public void Refresh() => Changed?.Invoke();
}
