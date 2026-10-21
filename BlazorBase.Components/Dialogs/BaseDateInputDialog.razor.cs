using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.Components.Dialogs;

public partial class BaseDateInputDialog : ComponentBase, IDialogContentComponent<DateInputDialogContent>
{
    [Inject]
    private IStringLocalizer<BaseDateInputDialog> Localizer { get; set; } = default!;

    [Parameter]
    public DateInputDialogContent Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private DateTime? SelectedDate;

    private string ConfirmText => Content.ConfirmButtonText ?? Localizer["Confirm"];

    private string CancelText => Content.CancelButtonText ?? Localizer["Cancel"];

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
