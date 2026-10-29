using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Data passed to BaseDialog when opening it from BaseList.
/// </summary>
public class BaseDialogData<TModel> : IAttachableDialogData where TModel : class
{
    private bool _closeRequested;
    private Func<Task>? _close;

    public TModel Model { get; set; } = default!;

    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    public bool IsNew { get; set; }

    /// <summary>
    /// Opens the card for viewing only: nothing can be edited and there is no save button.
    /// </summary>
    public bool ReadOnly { get; set; }

    public IStringLocalizer? Localizer { get; set; }

    public BaseCardConfiguration<TModel>? CardConfiguration { get; set; }

    public Type? CardType { get; set; }

    public List<CrudActionGroup<TModel>>? ActionGroups { get; set; }

    internal CardCloseGuard CloseGuard { get; } = new();

    /// <summary>
    /// Closes the dialog that shows this data with a cancelled result: at once when it is open, otherwise as soon
    /// as it has opened.
    /// </summary>
    public Task CloseAsync()
    {
        _closeRequested = true;

        return _close?.Invoke() ?? Task.CompletedTask;
    }

    internal bool IsCloseRequested => _closeRequested;

    internal Task AttachAsync(IDialogInstance instance) => Attach(instance.CancelAsync);

    Task IAttachableDialogData.AttachAsync(Func<Task> close) => Attach(close);

    private Task Attach(Func<Task> close)
    {
        _close = close;

        return _closeRequested ? close() : Task.CompletedTask;
    }
}

/// <summary>
/// The part of <see cref="BaseDialogData{TModel}"/> that does not depend on the model type.
/// </summary>
internal interface IAttachableDialogData
{
    Task AttachAsync(Func<Task> close);
}
