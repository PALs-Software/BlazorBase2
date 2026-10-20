namespace BlazorBase.CRUD.Validation;

/// <summary>
/// Holds the input errors a card editor detected before a value could even reach the model — for instance text that is
/// not a number. The card validator turns them into validation messages, so the form cannot be saved while one is pending.
/// </summary>
public sealed class CardFieldErrors
{
    private readonly Dictionary<string, string> errors = [];

    /// <summary>The pending errors by property name.</summary>
    public IReadOnlyDictionary<string, string> All => errors;

    /// <summary>Records <paramref name="message"/> for the property, replacing an earlier one.</summary>
    public void Set(string propertyName, string message) => errors[propertyName] = message;

    /// <summary>Removes the pending error of the property; returns whether there was one.</summary>
    public bool Clear(string propertyName) => errors.Remove(propertyName);

    /// <summary>Returns the pending error of the property, or null.</summary>
    public string? Get(string propertyName) => errors.GetValueOrDefault(propertyName);
}
