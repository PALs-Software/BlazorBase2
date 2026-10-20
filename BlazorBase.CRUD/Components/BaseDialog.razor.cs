using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Microsoft.JSInterop;

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
        if (Dialog is null)
            return;

        await CloseSafelyAsync(() => Dialog.CloseAsync(savedModel));
    }

    private async Task OnCancelledAsync()
    {
        if (Dialog is null)
            return;

        await CloseSafelyAsync(Dialog.CancelAsync);
    }

    /// <summary>
    /// Closes the dialog and tolerates the hosting provider being torn down while it does.
    /// </summary>
    /// <remarks>
    /// FluentUI dismisses the dialog first and hands focus back through the dialog provider's script
    /// module afterwards. A close that changes the URL (the deep-linked card strips its query parameter) lets
    /// the router rebuild the page, and the provider with it, in between; the hand-back then throws an
    /// <see cref="ObjectDisposedException"/> from a dialog that is already closed. Nothing is left to do at
    /// that point, so the exception is not allowed to escape into the card that asked for the close.
    /// </remarks>
    private static async Task CloseSafelyAsync(Func<Task> close)
    {
        try
        {
            await close();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or JSDisconnectedException)
        {
        }
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
