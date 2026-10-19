namespace BlazorBase.Components.Dialogs;

/// <summary>The date the user picked and confirmed in a <see cref="BaseDateInputDialog"/>.</summary>
public sealed class DateInputDialogResult
{
    public required DateTime Date { get; init; }
}
