using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Hosts a BaseCard in defer-save mode for inline-list editing.
/// Returns the changedFields dictionary as dialog result, or cancellation.
/// </summary>
public partial class BaseListPartItemDialog<TModel> : ComponentBase, IDialogContentComponent<BaseDialogData<TModel>>
    where TModel : class, new()
{
    [Parameter]
    public BaseDialogData<TModel> Content { get; set; } = default!;

    [CascadingParameter]
    private FluentDialog? Dialog { get; set; }

    private BaseDialogData<TModel> DialogData => Content;

    private BaseCardConfiguration<TModel>? CardConfiguration => DialogData.CardConfiguration;

    private async Task OnDeferredSaveAsync(Dictionary<string, object?> changedFields)
    {
        if (Dialog is not null)
            await Dialog.CloseAsync(changedFields);
    }

    private async Task OnCancelledAsync()
    {
        if (Dialog is not null)
            await Dialog.CancelAsync();
    }

    private Dictionary<string, object?> GetCustomCardParameters() => new()
    {
        ["Model"] = DialogData.Model,
        ["Localizer"] = DialogData.Localizer,
        ["DeferSave"] = true,
        ["OnDeferredSave"] = EventCallback.Factory.Create<Dictionary<string, object?>>(this, OnDeferredSaveAsync),
        ["OnCancelled"] = EventCallback.Factory.Create(this, OnCancelledAsync)
    };
}
