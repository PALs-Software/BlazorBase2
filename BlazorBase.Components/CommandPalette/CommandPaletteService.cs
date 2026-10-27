namespace BlazorBase.Components.CommandPalette;

/// <summary>The default <see cref="ICommandPaletteService"/>: plain state plus a change notification.</summary>
internal sealed class CommandPaletteService : ICommandPaletteService
{
    public bool IsOpen { get; private set; }

    public string? InitialQuery { get; private set; }

    public event Action? Changed;

    public Task OpenAsync(string? initialQuery = null)
    {
        if (IsOpen && initialQuery is null)
            return Task.CompletedTask;

        IsOpen = true;
        InitialQuery = initialQuery;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task CloseAsync()
    {
        if (!IsOpen)
            return Task.CompletedTask;

        IsOpen = false;
        InitialQuery = null;
        Changed?.Invoke();
        return Task.CompletedTask;
    }
}
