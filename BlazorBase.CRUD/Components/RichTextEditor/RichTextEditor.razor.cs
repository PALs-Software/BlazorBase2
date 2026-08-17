using BlazorBase.CRUD.Sanitization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorBase.CRUD.Components.RichTextEditor;

/// <summary>
/// A generic, vendored rich-text/HTML editor component that produces and consumes an HTML string.
/// Integrates with the BlazorBase.CRUD field pipeline via EditorTemplate / DisplayTemplate.
/// </summary>
public partial class RichTextEditor : IAsyncDisposable
{
    [Inject]
    private IJSRuntime JsRuntime { get; set; } = default!;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    /// <summary>The current HTML value (two-way bindable).</summary>
    [Parameter]
    public string? Value { get; set; }

    /// <summary>Raised whenever the editor content changes.</summary>
    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    /// <summary>Placeholder text shown when the editor is empty.</summary>
    [Parameter]
    public string? Placeholder { get; set; }

    /// <summary>When true the editor surface is read-only.</summary>
    [Parameter]
    public bool ReadOnly { get; set; }

    /// <summary>When true the whole component is visually disabled.</summary>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Optional label rendered above the editor.</summary>
    [Parameter]
    public string? Label { get; set; }

    /// <summary>Toolbar button label and aria-label for Bold. Defaults to "Bold".</summary>
    [Parameter]
    public string BoldLabel { get; set; } = "Bold";

    /// <summary>Toolbar button label and aria-label for Italic. Defaults to "Italic".</summary>
    [Parameter]
    public string ItalicLabel { get; set; } = "Italic";

    /// <summary>Toolbar button label and aria-label for Underline. Defaults to "Underline".</summary>
    [Parameter]
    public string UnderlineLabel { get; set; } = "Underline";

    /// <summary>Toolbar button label and aria-label for Bulleted list. Defaults to "Bulleted list".</summary>
    [Parameter]
    public string BulletListLabel { get; set; } = "Bulleted list";

    /// <summary>Toolbar button label and aria-label for Numbered list. Defaults to "Numbered list".</summary>
    [Parameter]
    public string NumberedListLabel { get; set; } = "Numbered list";

    /// <summary>Toolbar button label and aria-label for Insert link. Defaults to "Insert link".</summary>
    [Parameter]
    public string InsertLinkLabel { get; set; } = "Insert link";

    /// <summary>Toolbar button label and aria-label for Clear formatting. Defaults to "Clear formatting".</summary>
    [Parameter]
    public string ClearFormattingLabel { get; set; } = "Clear formatting";

    /// <summary>Text shown in the browser prompt when the user activates "Insert link". Defaults to "Enter URL:".</summary>
    [Parameter]
    public string InsertLinkPromptText { get; set; } = "Enter URL:";

    private string ElementId { get; } = $"rte-{Guid.NewGuid():N}";

    private RichTextEditorInterop? Interop;
    private DotNetObjectReference<RichTextEditor>? DotNetRef;
    private bool IsInitialized;
    private string? LastSyncedValue;

    protected override void OnInitialized()
    {
        Interop = new RichTextEditorInterop(JsRuntime);
        DotNetRef = DotNetObjectReference.Create(this);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || Interop is null || DotNetRef is null)
            return;

        try
        {
            await Interop.InitAsync(ElementId, DotNetRef, ReadOnly || Disabled, Placeholder, InsertLinkPromptText);
            await Interop.SetHtmlAsync(ElementId, SanitizeValue(Value));
            LastSyncedValue = Value;
            IsInitialized = true;
        }
        catch
        {
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!IsInitialized || Interop is null)
            return;

        try
        {
            await Interop.SetReadOnlyAsync(ElementId, ReadOnly || Disabled);

            if (Value != LastSyncedValue)
            {
                await Interop.SetHtmlAsync(ElementId, SanitizeValue(Value));
                LastSyncedValue = Value;
            }
        }
        catch
        {
        }
    }

    private string? SanitizeValue(string? html)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        var sanitizer = ServiceProvider.GetService(typeof(IHtmlSanitizer)) as IHtmlSanitizer;

        return sanitizer is not null ? sanitizer.Sanitize(html) : html;
    }

    /// <summary>
    /// Invoked by the JS module when the editor content changes (debounced ~200 ms).
    /// </summary>
    [JSInvokable]
    public async Task OnContentChangedAsync(string html)
    {
        var sanitizedHtml = SanitizeValue(html);
        LastSyncedValue = sanitizedHtml;
        Value = sanitizedHtml;

        if (ValueChanged.HasDelegate)
            await ValueChanged.InvokeAsync(sanitizedHtml);
    }

    public async ValueTask DisposeAsync()
    {
        if (IsInitialized && Interop is not null)
        {
            try { await Interop.DestroyAsync(ElementId); }
            catch { }
        }

        if (Interop is not null)
        {
            try { await Interop.DisposeAsync(); }
            catch { }
        }

        DotNetRef?.Dispose();
    }
}
