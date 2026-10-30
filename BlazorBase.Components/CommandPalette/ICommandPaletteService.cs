namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// Opens and closes the <see cref="BaseCommandPalette"/> from code, so any page or button can offer it. Scoped; the
/// palette itself is rendered by <c>BaseLayout</c> when <c>ShowCommandPalette</c> is set.
/// </summary>
public interface ICommandPaletteService
{
    /// <summary>True while the palette is open.</summary>
    bool IsOpen { get; }

    /// <summary>The text the palette was opened with, if any.</summary>
    string? InitialQuery { get; }

    /// <summary>Raised after <see cref="IsOpen"/> or <see cref="InitialQuery"/> changed.</summary>
    event Action? Changed;

    /// <summary>Opens the palette, optionally with a text already typed. An open palette takes the new text.</summary>
    Task OpenAsync(string? initialQuery = null);

    /// <summary>Closes the palette; does nothing when it is closed.</summary>
    Task CloseAsync();
}
