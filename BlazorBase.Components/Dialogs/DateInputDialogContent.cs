namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Configuration for a <see cref="BaseDateInputDialog"/>: a single date picked and confirmed by the
/// user, e.g. to scope a destructive action (such as purging a month of imported data) to one period.
/// </summary>
public sealed class DateInputDialogContent
{
    public required string Message { get; init; }

    public DateTime? InitialDate { get; init; }

    public string ConfirmButtonText { get; init; } = "Confirm";

    public string CancelButtonText { get; init; } = "Cancel";
}
