using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

public partial class BaseDateInputDialog : ComponentBase, IDialogContentComponent<DateInputDialogContent>
{
    [Parameter]
    public DateInputDialogContent Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private DateTime? SelectedDate;

    protected override void OnInitialized()
    {
        SelectedDate = Content.InitialDate;
    }

    private void OnDateChanged(DateTime? value)
    {
        SelectedDate = value;
    }

    private async Task ConfirmAsync()
    {
        if (Dialog is null || SelectedDate is null)
            return;

        await Dialog.CloseAsync(new DateInputDialogResult { Date = SelectedDate.Value });
    }

    private async Task CancelAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }
}
