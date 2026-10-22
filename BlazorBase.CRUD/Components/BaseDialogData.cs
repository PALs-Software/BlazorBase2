using BlazorBase.CRUD.Components.Internal;
using BlazorBase.CRUD.Configuration;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Models;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// Data passed to BaseDialog when opening it from BaseList.
/// </summary>
public class BaseDialogData<TModel> where TModel : class
{
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
}
