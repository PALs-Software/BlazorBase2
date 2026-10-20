using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.Components.Dialogs;

/// <summary>
/// Gives the dialog it is rendered in the behavior a modal dialog owes the keyboard: focus moves into it
/// when it opens, nothing behind it can be reached or tabbed to while it is open, and Escape commits a
/// half-typed field first so an unsaved-changes check sees what the user typed.
/// </summary>
/// <remarks>
/// FluentUI's own focus trap is switched off for the dialogs hosting a card, because two modal dialogs
/// stacked on each other (a card and its discard prompt) each pull focus back into themselves and fight
/// over it. Making everything outside the dialog <c>inert</c> needs no cooperation between the dialogs
/// and reaches into shadow roots, which a script walking the tab order cannot.
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

    /// <summary>
    /// Closes the dialog when Escape is pressed. FluentUI does this itself, but a dialog opened on top of another one
    /// never sees the key: the dialog beneath already handled it and marked it as handled.
    /// </summary>
    [Parameter]
    public bool DismissOnEscape { get; set; }

    private ElementReference RootElement { get; set; }
    private IJSObjectReference? Module { get; set; }
    private string? Token { get; set; }
    private bool IsDisposed { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        Module = await JsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
        Token = await Module.InvokeAsync<string?>("register", RootElement, new { initialFocus = InitialFocus, commitOnEscape = CommitPendingEditOnEscape, dismissOnEscape = DismissOnEscape });

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
