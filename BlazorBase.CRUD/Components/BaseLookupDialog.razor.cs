using BlazorBase.CRUD.Core;
using BlazorBase.CRUD.Localization;
using BlazorBase.CRUD.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

namespace BlazorBase.CRUD.Components;

public partial class BaseLookupDialog<TModel> : ComponentBase where TModel : class, new()
{
    #region Injects

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    #endregion

    private IStringLocalizer FrameworkLocalizer =>
        LocalizerResolver.ResolveFramework(ServiceProvider);

    [Parameter]
    public IBaseDataProvider<TModel>? DataProvider { get; set; }

    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    public string? DisplayPropertyName { get; set; }

    [Parameter]
    public EventCallback<TModel?> OnItemSelected { get; set; }

    private FluentDialog? Dialog { get; set; }
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

    private async Task ConfirmAsync()
    {
        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(SelectedItem);
    }

    private async Task CancelAsync()
    {
        if (OnItemSelected.HasDelegate)
            await OnItemSelected.InvokeAsync(null);
    }

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
