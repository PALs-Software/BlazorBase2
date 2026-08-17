namespace BlazorBase.CRUD.Components.CustomProperties;

/// <summary>
/// Optional save lifecycle contract. A custom input component (<see cref="IBaseCustomPropertyInput"/>)
/// implements this only when it needs save-time behavior such as cross-field validation or revealing
/// a freshly generated value after persistence. The card runs <see cref="ValidateAsync"/> on every
/// collected participant (aborting the save if any returns false), then <see cref="OnBeforeSaveAsync"/>
/// before persisting, then <see cref="OnAfterSaveAsync"/> after persisting.
/// </summary>
public interface ICardSaveParticipant
{
    /// <summary>
    /// Returns false to block the save. The component renders its own feedback; it may also surface
    /// a message through the card via <see cref="OnBeforeSaveAsync"/>/<see cref="OnAfterSaveAsync"/>.
    /// </summary>
    Task<bool> ValidateAsync();

    /// <summary>Runs before the card persists the model.</summary>
    Task OnBeforeSaveAsync(CardSaveContext context);

    /// <summary>Runs after the card has persisted the model.</summary>
    Task OnAfterSaveAsync(CardSaveContext context);
}
