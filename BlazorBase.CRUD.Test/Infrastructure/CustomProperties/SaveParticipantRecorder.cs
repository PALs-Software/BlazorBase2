using BlazorBase.CRUD.Components.CustomProperties;

namespace BlazorBase.CRUD.Test.Infrastructure.CustomProperties;

/// <summary>
/// Captures the save lifecycle calls made on <see cref="SaveParticipantInput"/> so tests can assert
/// ordering and whether the save was blocked. Configure <see cref="ValidationResult"/> to drive
/// <see cref="ICardSaveParticipant.ValidateAsync"/>.
/// </summary>
public sealed class SaveParticipantRecorder
{
    public bool ValidationResult { get; set; } = true;

    public List<string> Calls { get; } = [];

    public CardSaveContext? BeforeSaveContext { get; set; }

    public CardSaveContext? AfterSaveContext { get; set; }
}
