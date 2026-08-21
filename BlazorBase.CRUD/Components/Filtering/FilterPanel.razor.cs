using BlazorBase.CRUD.Filtering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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

    private ElementReference PanelElement;

    /// <summary>
    /// Moves focus into the panel when it opens, so the keyboard follows the slide-over instead of
    /// staying behind it on the list.
    /// </summary>
    /// <remarks>
    /// The panel covers the list behind an overlay and announces itself as <c>aria-modal</c>. Without
    /// this the promise is false: a reader is told the rest of the page is inert while the keyboard is
    /// still standing in it, and the first Tab walks through the covered list rather than the filter.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        await PanelElement.FocusAsync(preventScroll: true);
    }

    /// <summary>
    /// Escape closes the panel, the way every other modal surface behaves.
    /// </summary>
    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key != "Escape")
            return;

        await OnClose.InvokeAsync();
    }

    private async Task ApplyAsync() => await OnApply.InvokeAsync(Root);

    private async Task ResetAsync() => await OnReset.InvokeAsync();
}
