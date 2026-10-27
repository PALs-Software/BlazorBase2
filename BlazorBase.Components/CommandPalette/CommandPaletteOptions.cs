namespace BlazorBase.Components.CommandPalette;

/// <summary>
/// Tuning of the command palette. Configure it through <c>AddBlazorBaseCommandPalette</c>.
/// </summary>
public sealed class CommandPaletteOptions
{
    /// <summary>The most results shown in total.</summary>
    public int MaxResults { get; set; } = 30;

    /// <summary>The most results shown per group while the user has typed a query.</summary>
    public int MaxResultsPerGroup { get; set; } = 6;

    /// <summary>The most suggestions shown per group while the query is empty.</summary>
    public int MaxSuggestionsPerGroup { get; set; } = 5;

    /// <summary>The most recently used entries remembered and listed under "Recent".</summary>
    public int MaxRecent { get; set; } = 5;

    /// <summary>How long the palette waits after a keystroke before it asks the providers.</summary>
    public TimeSpan SearchDebounce { get; set; } = TimeSpan.FromMilliseconds(120);

    /// <summary>How long a provider may take before it is skipped for this search.</summary>
    public TimeSpan ProviderTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a search runs before the palette shows a loading indicator.</summary>
    public TimeSpan LoadingIndicatorDelay { get; set; } = TimeSpan.FromMilliseconds(300);

    /// <summary>Lets a plain "/" open the palette while no editable element has the focus.</summary>
    public bool SlashOpensPalette { get; set; } = true;
}
