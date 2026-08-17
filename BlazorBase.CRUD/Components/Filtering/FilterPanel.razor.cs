using BlazorBase.CRUD.Filtering;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace BlazorBase.CRUD.Components.Filtering;

public partial class FilterPanel
{
    [Parameter, EditorRequired]
    public IReadOnlyList<FilterFieldMetadata> Fields { get; set; } = [];

    [Parameter, EditorRequired]
    public FilterGroupModel Root { get; set; } = default!;

    [Parameter, EditorRequired]
    public IStringLocalizer Localizer { get; set; } = default!;

    [Parameter]
    public EventCallback<FilterGroupModel> OnApply { get; set; }

    [Parameter]
    public EventCallback OnReset { get; set; }

    [Parameter]
    public EventCallback OnClose { get; set; }

    private async Task ApplyAsync() => await OnApply.InvokeAsync(Root);

    private async Task ResetAsync() => await OnReset.InvokeAsync();
}
