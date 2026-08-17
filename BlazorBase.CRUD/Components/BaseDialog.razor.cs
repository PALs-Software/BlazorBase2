using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

public partial class BaseDialog<TModel> : ComponentBase, IDialogContentComponent<BaseDialogData<TModel>>
    where TModel : class, new()
{
    [Parameter]
    public BaseDialogData<TModel> Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private BaseDialogData<TModel> DialogData => Content;

    private BaseCardConfiguration<TModel>? CardConfiguration => DialogData.CardConfiguration;

    private async Task OnSavedAsync(TModel savedModel)
    {
        if (Dialog is not null)
            await Dialog.CloseAsync(savedModel);
    }

    private async Task OnCancelledAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }

    private Dictionary<string, object?> GetCustomCardParameters() => new()
    {
        ["Model"] = DialogData.Model,
        ["DataProvider"] = DialogData.DataProvider,
        ["Localizer"] = DialogData.Localizer,
        ["Configuration"] = DialogData.CardConfiguration,
        ["OnAfterSave"] = EventCallback.Factory.Create<TModel>(this, OnSavedAsync),
        ["OnCancelled"] = EventCallback.Factory.Create(this, OnCancelledAsync)
    };
}
