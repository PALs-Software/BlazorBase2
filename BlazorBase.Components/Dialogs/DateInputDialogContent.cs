namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Configuration for a <see cref="BaseDateInputDialog"/>: a single date picked and confirmed by the
/// user, e.g. to scope a destructive action (such as purging a month of imported data) to one period.
/// </summary>
public sealed class DateInputDialogContent
{
    public required string Message { get; init; }

    public DateTime? InitialDate { get; init; }

    /// <summary>Caption of the confirm button; the dialog's localized "Confirm" when null.</summary>
    public string? ConfirmButtonText { get; init; }

    /// <summary>Caption of the cancel button; the dialog's localized "Cancel" when null.</summary>
    public string? CancelButtonText { get; init; }
}
