using BlazorBase.Components.Dialogs;
using BlazorBase.CRUD.Configuration;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The content of the dialog that hosts a card, opened with <see cref="CardDialogServiceExtensions.ShowCardDialogAsync{TModel}"/>.
/// </summary>
public partial class BaseDialog<TModel>
    where TModel : class, new()
{
    private BaseDialogData<TModel> DialogData => Content;

    private BaseCardConfiguration<TModel>? CardConfiguration => DialogData.CardConfiguration;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
        await DialogData.AttachAsync(DialogInstance);
    }

    private Task OnSavedAsync(TModel savedModel) => CloseSafelyAsync(() => ConfirmWithResultAsync(savedModel));

    private Task OnCancelledAsync() => CloseSafelyAsync(CancelAsync);

    /// <summary>
    /// Closes the dialog and tolerates the hosting provider being torn down while it does.
    /// </summary>
    /// <remarks>
    /// The dialog is dismissed first and focus is handed back through the dialog provider's script
    /// module afterwards. A close that changes the URL (the deep-linked card strips its query parameter) lets
    /// the router rebuild the page, and the provider with it, in between; the hand-back then throws an
    /// <see cref="ObjectDisposedException"/> from a dialog that is already closed. Nothing is left to do at
    /// that point, so the exception is not allowed to escape into the card that asked for the close.
    /// </remarks>
    internal static async Task CloseSafelyAsync(Func<Task> close)
    {
        try
        {
            await close();
        }
        catch (Exception ex) when (ex is ObjectDisposedException or JSDisconnectedException)
        {
        }
    }

    private Dictionary<string, object?> GetCustomCardParameters()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["Model"] = DialogData.Model,
            ["DataProvider"] = DialogData.DataProvider,
            ["Localizer"] = DialogData.Localizer,
            ["Configuration"] = DialogData.CardConfiguration,
            ["OnAfterSave"] = EventCallback.Factory.Create<TModel>(this, OnSavedAsync),
            ["OnCancelled"] = EventCallback.Factory.Create(this, OnCancelledAsync)
        };

        if (DialogData.ReadOnly && DeclaresReadOnlyParameter(DialogData.CardType))
            parameters["ReadOnly"] = true;

        return parameters;
    }

    /// <summary>
    /// Whether a custom card type can be told to show read-only: only then can a user without edit rights be allowed to
    /// open it.
    /// </summary>
    internal static bool DeclaresReadOnlyParameter(Type? cardType)
        => cardType?.GetProperty("ReadOnly")?.IsDefined(typeof(ParameterAttribute), inherit: true) == true;
}
