namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Context handed to an <see cref="ICardSaveParticipant"/> during the card save lifecycle. Carries
/// the model, whether this is a create, and a channel to surface a message back to the card.
/// </summary>
/// <param name="model">The model being saved.</param>
/// <param name="isCreate">True when the model is being created (new), false when updated.</param>
public sealed class CardSaveContext(object model, bool isCreate)
{
    /// <summary>The model being saved.</summary>
    public object Model { get; } = model;

    /// <summary>True when the model is being created (new), false when updated.</summary>
    public bool IsCreate { get; } = isCreate;

    /// <summary>
    /// A message surfaced to the card (e.g. validation feedback or a one-time generated secret).
    /// The last non-null message set by a participant is shown by the card.
    /// </summary>
    public string? Message { get; private set; }

    /// <summary>Sets the message surfaced to the card.</summary>
    public void AddMessage(string message) => Message = message;
}
