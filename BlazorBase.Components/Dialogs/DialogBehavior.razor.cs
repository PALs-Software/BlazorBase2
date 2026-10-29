using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Gives the dialog it is rendered in the keyboard behavior a modal dialog owes the user: focus moves into it when
/// it opens, and Escape commits a half-typed field first so an unsaved-changes check sees what the user typed.
/// </summary>
/// <remarks>
/// The page behind the dialog is made inert, Escape closes it and focus returns to the opener by the dialog itself,
/// which is a native modal. The dialog has to carry the <see cref="BaseDialogOptions.DialogClass"/> class, which
/// <c>ShowBaseDialogAsync</c> sets.
/// </remarks>
public partial class DialogBehavior : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/BlazorBase.Components/js/baseDialogBehavior.js";

    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>The dialog content.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>CSS class of the wrapper element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Moves focus into the dialog when it opens. On by default.</summary>
    [Parameter]
    public bool InitialFocus { get; set; } = true;

    /// <summary>Commits the focused text field when Escape is pressed. On by default.</summary>
    [Parameter]
    public bool CommitPendingEditOnEscape { get; set; } = true;

    private ElementReference RootElement { get; set; }
    private IJSObjectReference? Module { get; set; }
    private string? Token { get; set; }
    private bool IsDisposed { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        Token = await Module.InvokeAsync<string?>("register", RootElement, new { initialFocus = InitialFocus, commitOnEscape = CommitPendingEditOnEscape });

        if (IsDisposed)
            await ReleaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        IsDisposed = true;
        await ReleaseAsync();
    }

    private async Task ReleaseAsync()
    {
        var module = Module;

        if (module is null)
            return;

        Module = null;

        try
        {
            if (Token is not null)
                await module.InvokeVoidAsync("unregister", Token);

            await module.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or OperationCanceledException)
        {
        }
    }
}
