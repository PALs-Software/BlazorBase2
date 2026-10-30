using BlazorBase.Components.Dialogs;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Hosts a BaseCard in defer-save mode for inline-list editing.
/// Returns the changedFields dictionary as dialog result, or cancellation.
/// </summary>
public partial class BaseListPartItemDialog<TModel>
    where TModel : class, new()
{
    private BaseDialogData<TModel> DialogData => Content;

    private BaseCardConfiguration<TModel>? CardConfiguration => DialogData.CardConfiguration;

    private Task OnDeferredSaveAsync(Dictionary<string, object?> changedFields) => ConfirmWithResultAsync(changedFields);

    private Task OnCancelledAsync() => CancelAsync();

    private Dictionary<string, object?> GetCustomCardParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Model"] = DialogData.Model,
            ["Localizer"] = DialogData.Localizer,
            ["DeferSave"] = true,
            ["OnDeferredSave"] = EventCallback.Factory.Create<Dictionary<string, object?>>(this, OnDeferredSaveAsync),
            ["OnCancelled"] = EventCallback.Factory.Create(this, OnCancelledAsync)
        };

        if (DialogData.ReadOnly && BaseDialog<TModel>.DeclaresReadOnlyParameter(DialogData.CardType))
            parameters["ReadOnly"] = true;

        return parameters;
    }
}
