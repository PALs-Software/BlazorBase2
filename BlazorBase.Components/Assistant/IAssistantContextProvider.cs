namespace BlazorBase.Components.Assistant;

/// <summary>
/// Opt-in contract for a component or service that tells the assistant about the page. Register it with
/// <see cref="AssistantContextService.Register"/>; <see cref="Contribute"/> runs each time a message is sent, so it
/// reports the state at that moment.
/// </summary>
public interface IAssistantContextProvider
{
    /// <summary>Adds the hints of this provider; ids and technical values only, never field values.</summary>
    void Contribute(AssistantContextBuilder builder);
}

/// <summary>Collects the hints of the providers for one <see cref="AssistantPageContext"/>.</summary>
public sealed class AssistantContextBuilder
{
    private readonly Dictionary<string, string> HintValues = new(StringComparer.Ordinal);

    /// <summary>The entity type; a later provider replaces the value of an earlier one.</summary>
    public string? EntityType { get; set; }

    /// <summary>The key of the selected record; a later provider replaces the value of an earlier one.</summary>
    public string? EntityKey { get; set; }

    /// <summary>The active filters; a later provider replaces the value of an earlier one.</summary>
    public string? Filters { get; set; }

    /// <summary>The active list preset; a later provider replaces the value of an earlier one.</summary>
    public string? Preset { get; set; }

    /// <summary>The visible error text; a later provider replaces the value of an earlier one.</summary>
    public string? ErrorText { get; set; }

    internal IReadOnlyDictionary<string, string> Hints => HintValues;

    /// <summary>Adds or replaces a hint; a blank key or value is ignored.</summary>
    public void AddHint(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrEmpty(value))
            return;

        HintValues[key.Trim()] = value;
    }
}
