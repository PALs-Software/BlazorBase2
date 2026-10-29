using BlazorBase.Components.Dialogs;
using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

/// <summary>
/// The content of the lookup dialog that lets the user search and pick one item of a data provider. Open it with
/// <see cref="BaseLookupDialogServiceExtensions.ShowLookupDialogAsync{TModel}"/>.
/// </summary>
public partial class BaseLookupDialog<TModel> where TModel : class, new()
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    /// <summary>The provider the items are searched in.</summary>
    [Parameter]
    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    /// <summary>The property shown for each item and searched with the entered text.</summary>
    [Parameter]
    public string? DisplayPropertyName { get; set; }

    private List<TModel> LoadedItems { get; set; } = [];
    private TModel? SelectedItem { get; set; }
    private string? SearchText { get; set; }
    private int TotalCount { get; set; }
    private bool IsLoading { get; set; }
    private bool HasMoreItems => LoadedItems.Count < TotalCount;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadDataAsync();
    }

    private async Task LoadDataAsync(bool reset = true)
    {
        if (DataProvider is null)
            return;

        IsLoading = true;
        StateHasChanged();

        if (reset)
            LoadedItems.Clear();

        var query = new BaseQuery
        {
            Skip = reset ? 0 : LoadedItems.Count,
            Take = 50
        };

        if (!string.IsNullOrWhiteSpace(SearchText) && DisplayPropertyName is not null)
        {
            query.Filters.Add(new FilterDescriptor
            {
                PropertyName = DisplayPropertyName,
                Operator = FilterOperator.Contains,
                Value = SearchText
            });
        }

        var result = await DataProvider.GetListAsync(query);
        TotalCount = result.TotalCount;

        if (reset)
            LoadedItems = result.Items;
        else
            LoadedItems.AddRange(result.Items);

        IsLoading = false;
        StateHasChanged();
    }

    private async Task LoadMoreAsync()
    {
        await LoadDataAsync(reset: false);
    }

    private async Task OnSearchChangedAsync()
    {
        await LoadDataAsync();
    }

    private void SelectItem(TModel item)
    {
        SelectedItem = item;
        StateHasChanged();
    }

    private Task ConfirmSelectionAsync() =>
        SelectedItem is null ? Task.CompletedTask : ConfirmWithResultAsync(SelectedItem);

    private string GetDisplayText(TModel item)
    {
        if (DisplayPropertyName is not null)
        {
            var prop = typeof(TModel).GetProperty(DisplayPropertyName);

            if (prop is not null)
                return prop.GetValue(item)?.ToString() ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Opens the lookup dialog.
/// </summary>
public static class BaseLookupDialogServiceExtensions
{
    /// <summary>
    /// Opens a <see cref="BaseLookupDialog{TModel}"/> and waits for it to close. A picked item is returned as
    /// <see cref="DialogResult.Value"/>; closing without a pick is a cancelled result.
    /// </summary>
    /// <param name="dialogService">The dialog service.</param>
    /// <param name="title">The title of the dialog.</param>
    /// <param name="dataProvider">The provider the items are searched in.</param>
    /// <param name="displayPropertyName">The property shown for each item and searched with the entered text.</param>
    public static Task<DialogResult> ShowLookupDialogAsync<TModel>(
        this IDialogService dialogService,
        string title,
        IBaseDataProvider<TModel> dataProvider,
        string? displayPropertyName = null)
        where TModel : class, new()
    {
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(dataProvider);

        return dialogService.ShowBaseDialogAsync<BaseLookupDialog<TModel>>(options =>
        {
            options.Title = title;
            options.Parameters[nameof(BaseLookupDialog<TModel>.DataProvider)] = dataProvider;
            options.Parameters[nameof(BaseLookupDialog<TModel>.DisplayPropertyName)] = displayPropertyName;
        });
    }
}
