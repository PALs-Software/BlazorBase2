namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Configuration for a <see cref="BaseConfirmationDialog"/>: the question, the two button captions and
/// whether the action it confirms is destructive. Shown via
/// <see cref="Services.FluentUiConfirmationDialogExtensions.ShowConfirmationDialogAsync"/>.
/// </summary>
public sealed class ConfirmationDialogContent
{
    public required string Message { get; init; }

    public required string ConfirmButtonText { get; init; }

    public required string CancelButtonText { get; init; }

    /// <summary>
    /// A destructive action gets a warning icon, a danger-styled confirm button, and starts on the
    /// cancel button so a stray Enter cannot confirm it.
    /// </summary>
    public bool IsDestructive { get; init; }
}
