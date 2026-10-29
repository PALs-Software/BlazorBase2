using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.Components.Dialogs;

public partial class BaseDateInputDialog : BaseDialogInstance<DateInputDialogContent>
{
    [Inject]
    private IStringLocalizer<BaseDateInputDialog> DialogLocalizer { get; set; } = default!;

    private DateTime? SelectedDate;

    private string ConfirmText => Content.ConfirmButtonText ?? DialogLocalizer["Confirm"];

    private string CancelText => Content.CancelButtonText ?? DialogLocalizer["Cancel"];

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        SelectedDate = Content.InitialDate;
    }

    private void OnDateChanged(DateTime? value)
    {
        SelectedDate = value;
    }

    private async Task ConfirmDateAsync()
    {
        if (SelectedDate is null)
            return;

        await ConfirmWithResultAsync(new DateInputDialogResult { Date = SelectedDate.Value });
    }
}
